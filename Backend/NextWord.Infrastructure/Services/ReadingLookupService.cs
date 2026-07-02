using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Domain.Services;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

public sealed class ReadingLookupService(
    ApplicationDbContext db,
    IUserLlmProviderFactory llmFactory,
    IScoreProfileService scoreProfile) : IReadingLookupService
{
    public async Task<ReadingLookupResponse> LookupAsync(Guid userId, ReadingLookupRequest request, CancellationToken cancellationToken)
    {
        var lemma = request.Word.Trim().ToLowerInvariant();
        var word = await db.Words.AsNoTracking()
            .Include(item => item.LlmAnnotation)
            .FirstOrDefaultAsync(item => item.Lemma == lemma, cancellationToken);
        var progress = await db.UserProgress.AsNoTracking().FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
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
        string contextDefinition;
        try
        {
            var llm = await llmFactory.GetForUserAsync(userId, cancellationToken);
            var snippet = request.Sentence.Length > 500 ? request.Sentence[..500] : request.Sentence;
            var definition = await llm.GetDefinitionAsync(new DefinitionRequest(
                lemma,
                snippet,
                new LlmRequestOptions("reading-lookup", "reading_lookup")), cancellationToken);
            contextDefinition = definition.Meanings.FirstOrDefault()?.Definition
                ?? word?.Meanings.FirstOrDefault()
                ?? lemma;
            offline = string.IsNullOrWhiteSpace(definition.Meanings.FirstOrDefault()?.Definition);
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
            word?.Phonetics,
            offline,
            confidence,
            null);
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
