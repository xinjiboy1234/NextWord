using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Domain.Services;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

public sealed class ArticleVocabService(
    ApplicationDbContext db,
    IUserLlmProviderFactory llmFactory,
    IUserRepository users,
    IOptions<LlmSentenceRatingOptions> sentenceRatingOptions) : IArticleVocabService
{
    public async Task<IReadOnlyList<ArticleVocabMapping>> GetMappingsAsync(Guid articleId, CancellationToken cancellationToken)
    {
        return await db.ArticleVocabMappings
            .AsNoTracking()
            .Where(mapping => mapping.ArticleId == articleId)
            .OrderByDescending(mapping => mapping.IsKeyVocab)
            .ThenBy(mapping => mapping.WordLemma)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ArticleVocabMapping>> ExtractAndPersistAsync(Guid articleId, Guid userId, CancellationToken cancellationToken)
    {
        var article = await db.Articles.FirstOrDefaultAsync(item => item.Id == articleId, cancellationToken)
            ?? throw new InvalidOperationException("Article not found.");

        var progress = await users.GetOrCreateProgressAsync(userId, cancellationToken);
        var existing = await db.ArticleVocabMappings.Where(mapping => mapping.ArticleId == articleId).ToListAsync(cancellationToken);
        if (existing.Count > 0)
        {
            return existing;
        }

        var explanationLanguage = ExplanationLanguageHelper.Resolve(
            null,
            sentenceRatingOptions.Value.ExplanationLanguage);
        var llm = await llmFactory.GetForUserAsync(userId, cancellationToken);
        var extraction = await llm.ExtractVocabAsync(new VocabExtractRequest(
            article.Title,
            article.Content,
            article.CefrLevel.ToString(),
            progress.ReadingLevel.ToString(),
            new LlmRequestOptions("reading-agent", "vocab_extract"),
            explanationLanguage), cancellationToken);

        var words = await db.Words.AsNoTracking().ToListAsync(cancellationToken);
        var mappings = extraction.KeyVocab.Select(item =>
        {
            var matchedWord = words.FirstOrDefault(word =>
                string.Equals(word.Lemma, item.Word, StringComparison.OrdinalIgnoreCase));
            var examples = WordExampleJson.FromKeyVocabItem(item);
            return new ArticleVocabMapping
            {
                ArticleId = articleId,
                WordId = matchedWord?.Id,
                WordLemma = item.Word.Trim().ToLowerInvariant(),
                ContextMeaning = item.ContextMeaning,
                Phonetics = item.Phonetics,
                ExamplesJson = WordExampleJson.Serialize(examples),
                SpecialUsage = string.Empty,
                DifficultyInContext = item.Difficulty,
                RecommendedAction = item.Action,
                IsKeyVocab = true
            };
        }).ToList();

        db.ArticleVocabMappings.AddRange(mappings);
        await db.SaveChangesAsync(cancellationToken);
        return mappings;
    }

    public async Task<ArticleWordDetailResult> GetOrCreateWordDetailAsync(
        Guid articleId,
        Guid userId,
        string word,
        string? context,
        CancellationToken cancellationToken)
    {
        var lemma = word.Trim().ToLowerInvariant();
        var mapping = await db.ArticleVocabMappings
            .FirstOrDefaultAsync(item => item.ArticleId == articleId && item.WordLemma == lemma, cancellationToken);

        if (mapping is not null && WordExampleJson.IsEnriched(mapping))
        {
            return new ArticleWordDetailResult(MapToDefinition(mapping, context), true);
        }

        var explanationLanguage = ExplanationLanguageHelper.Resolve(
            null,
            sentenceRatingOptions.Value.ExplanationLanguage);
        var llm = await llmFactory.GetForUserAsync(userId, cancellationToken);
        var snippet = string.IsNullOrWhiteSpace(context)
            ? context
            : context.Length > 500 ? context[..500] : context;
        var definition = await llm.GetDefinitionAsync(new DefinitionRequest(
            lemma,
            snippet,
            new LlmRequestOptions("reading-lookup", "reading_lookup"),
            explanationLanguage), cancellationToken);

        if (mapping is null)
        {
            var matchedWord = await db.Words.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Lemma == lemma, cancellationToken);
            mapping = new ArticleVocabMapping
            {
                ArticleId = articleId,
                WordId = matchedWord?.Id,
                WordLemma = lemma,
                IsKeyVocab = false
            };
            db.ArticleVocabMappings.Add(mapping);
        }

        ApplyDefinitionToMapping(mapping, definition, preserveContextMeaning: mapping.IsKeyVocab);
        await db.SaveChangesAsync(cancellationToken);
        return new ArticleWordDetailResult(definition, false);
    }

    public Task<ArticleWordDetailResult> LookupWordAsync(
        Guid articleId,
        Guid userId,
        string word,
        string? context,
        CancellationToken cancellationToken) =>
        GetOrCreateWordDetailAsync(articleId, userId, word, context, cancellationToken);

    internal static DefinitionResponse MapToDefinition(ArticleVocabMapping mapping, string? context)
    {
        var examples = WordExampleJson.Deserialize(mapping.ExamplesJson);
        return new DefinitionResponse(
            mapping.WordLemma,
            mapping.Phonetics,
            [new Meaning(mapping.ContextMeaning, true, context ?? string.Empty)],
            [],
            examples,
            mapping.SpecialUsage,
            mapping.DifficultyInContext,
            CefrLevel.A2);
    }

    internal static void ApplyDefinitionToMapping(
        ArticleVocabMapping mapping,
        DefinitionResponse definition,
        bool preserveContextMeaning)
    {
        if (!preserveContextMeaning)
        {
            mapping.ContextMeaning = definition.Meanings.FirstOrDefault()?.Definition ?? mapping.ContextMeaning;
        }

        mapping.Phonetics = definition.Phonetics;
        mapping.ExamplesJson = WordExampleJson.Serialize(definition.Examples);
        mapping.SpecialUsage = definition.SpecialUsage;
        mapping.DifficultyInContext = definition.DifficultyLevel;
    }
}
