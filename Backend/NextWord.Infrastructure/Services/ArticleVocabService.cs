using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

public sealed class ArticleVocabService(
    ApplicationDbContext db,
    IUserLlmProviderFactory llmFactory,
    ILLMProvider globalLlm,
    IUserRepository users) : IArticleVocabService
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

        var llm = await llmFactory.GetForUserAsync(userId, cancellationToken);
        var extraction = await llm.ExtractVocabAsync(new VocabExtractRequest(
            article.Title,
            article.Content,
            article.CefrLevel.ToString(),
            progress.ReadingLevel.ToString(),
            new LlmRequestOptions("reading-agent", "vocab_extract")), cancellationToken);

        var words = await db.Words.AsNoTracking().ToListAsync(cancellationToken);
        var mappings = extraction.KeyVocab.Select(item =>
        {
            var matchedWord = words.FirstOrDefault(word =>
                string.Equals(word.Lemma, item.Word, StringComparison.OrdinalIgnoreCase));
            return new ArticleVocabMapping
            {
                ArticleId = articleId,
                WordId = matchedWord?.Id,
                WordLemma = item.Word.Trim().ToLowerInvariant(),
                ContextMeaning = item.ContextMeaning,
                SpecialUsage = item.SpecialUsage,
                DifficultyInContext = item.Difficulty,
                RecommendedAction = item.Action,
                IsKeyVocab = true
            };
        }).ToList();

        db.ArticleVocabMappings.AddRange(mappings);
        await db.SaveChangesAsync(cancellationToken);
        return mappings;
    }

    public async Task<DefinitionResponse?> LookupWordAsync(Guid articleId, string word, string? context, CancellationToken cancellationToken)
    {
        var lemma = word.Trim().ToLowerInvariant();
        var cached = await db.ArticleVocabMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(mapping => mapping.ArticleId == articleId && mapping.WordLemma == lemma, cancellationToken);

        if (cached is not null)
        {
            return new DefinitionResponse(
                cached.WordLemma,
                string.Empty,
                [new Meaning(cached.ContextMeaning, true, context ?? string.Empty)],
                [],
                [],
                cached.SpecialUsage,
                cached.DifficultyInContext,
                CefrLevel.A2);
        }

        return await globalLlm.GetDefinitionAsync(new DefinitionRequest(
            lemma,
            context,
            new LlmRequestOptions("reading-agent", "word_lookup")), cancellationToken);
    }
}
