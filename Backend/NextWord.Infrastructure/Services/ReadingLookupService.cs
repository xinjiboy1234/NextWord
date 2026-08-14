using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Domain.Services;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

public sealed class ReadingLookupService(
    ApplicationDbContext db,
    IArticleVocabService articleVocab,
    IUserLlmProviderFactory llmFactory,
    IOptions<LlmSentenceRatingOptions> sentenceRatingOptions) : IReadingLookupService
{
    public async Task<ReadingLookupResponse> LookupAsync(Guid userId, ReadingLookupRequest request, CancellationToken cancellationToken)
    {
        var lemma = request.Word.Trim().ToLowerInvariant();
        var word = await db.Words.AsNoTracking()
            .Include(item => item.LlmAnnotation)
            .FirstOrDefaultAsync(item => item.Lemma == lemma, cancellationToken);
        var relationship = word is null
            ? null
            : await db.UserWordRelationships.AsNoTracking()
                .FirstOrDefaultAsync(item => item.UserId == userId && item.WordId == word.Id, cancellationToken);

        var annotation = word?.LlmAnnotation;
        var intrinsic = annotation?.IntrinsicScore ?? LegacyScoreHelper.FromDifficulty(word?.DifficultyLevel ?? DifficultyLevel.Intermediate);
        var confidence = annotation?.Confidence ?? 0.5;

        var effective = EffectiveDifficultyCalculator.Compute(
            intrinsic,
            LegacyScoreHelper.FromDifficulty(word?.DifficultyLevel ?? DifficultyLevel.Intermediate),
            relationship,
            new ReadingDifficultyContext(null));

        var offline = false;
        var fromCache = false;
        string contextDefinition;
        string? phonetic = word?.Phonetics;
        string? specialUsage = null;
        IReadOnlyList<WordExampleDto>? examples = null;

        try
        {
            var snippet = request.Sentence.Length > 500 ? request.Sentence[..500] : request.Sentence;
            DefinitionResponse definition;

            if (request.ArticleId is Guid articleId)
            {
                var detail = await articleVocab.GetOrCreateWordDetailAsync(
                    articleId,
                    userId,
                    lemma,
                    snippet,
                    cancellationToken);
                definition = detail.Definition;
                fromCache = detail.FromCache;
            }
            else
            {
                var explanationLanguage = ExplanationLanguageHelper.Resolve(
                    null,
                    sentenceRatingOptions.Value.ExplanationLanguage);
                var llm = await llmFactory.GetForUserAsync(userId, cancellationToken);
                definition = await llm.GetDefinitionAsync(new DefinitionRequest(
                    lemma,
                    snippet,
                    new LlmRequestOptions("reading-lookup", "reading_lookup"),
                    explanationLanguage), cancellationToken);
            }

            contextDefinition = definition.Meanings.FirstOrDefault()?.Definition
                ?? word?.Meanings.FirstOrDefault()
                ?? lemma;
            phonetic = string.IsNullOrWhiteSpace(definition.Phonetics) ? phonetic : definition.Phonetics;
            specialUsage = definition.SpecialUsage;
            examples = definition.Examples.Select(WordExampleDto.FromModel).ToList();
            // T-049：降级内容（Mock 占位 / LLM 失败回退）按离线处理，响应 Offline=true 让前端可见提示
            offline = definition.IsFallback || string.IsNullOrWhiteSpace(definition.Meanings.FirstOrDefault()?.Definition);
        }
        catch
        {
            offline = true;
            contextDefinition = word?.Meanings.FirstOrDefault() ?? lemma;
        }

        if (offline)
        {
            contextDefinition = $"[离线模式] {contextDefinition}";
        }

        return new ReadingLookupResponse(
            lemma,
            contextDefinition,
            intrinsic,
            relationship?.PersonalDifficulty ?? (relationship is null ? null : effective.Score),
            relationship?.EstimatedKnownRate ?? 0.5,
            phonetic,
            offline,
            confidence,
            null,
            specialUsage,
            examples,
            fromCache);
    }
}

internal static class LegacyScoreHelper
{
    public static int FromDifficulty(DifficultyLevel level) => level switch
    {
        DifficultyLevel.Basic => 25,
        DifficultyLevel.Intermediate => 50,
        DifficultyLevel.Advanced => 75,
        _ => 40
    };
}
