using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

public sealed class DailyWordSelectionService(ApplicationDbContext db, IScoreProfileService scoreProfile) : IDailyWordSelectionService
{
    public async Task<IReadOnlyList<DailyWordItem>> GetDailyAsync(Guid userId, int count, CancellationToken cancellationToken)
    {
        count = Math.Clamp(count, 1, 20);
        var scores = await scoreProfile.GetScoresAsync(userId, cancellationToken);
        var vocabScore = scores.Vocabulary ?? 42;
        var min = vocabScore;
        var max = Math.Min(100, vocabScore + 12);

        var learnedIds = await db.UserWordRelationships
            .Where(item => item.UserId == userId)
            .Select(item => item.WordId)
            .ToListAsync(cancellationToken);

        var weak = await db.UserWordRelationships
            .AsNoTracking()
            .Include(item => item.Word)
            .Where(item => item.UserId == userId && item.EstimatedKnownRate < 0.4 && item.Word != null)
            .OrderBy(item => item.EstimatedKnownRate)
            .Take(count / 2)
            .ToListAsync(cancellationToken);

        var candidates = await db.Words.AsNoTracking()
            .Include(word => word.LlmAnnotation)
            .Where(word => !learnedIds.Contains(word.Id))
            .ToListAsync(cancellationToken);

        var bandWords = candidates
            .Select(word =>
            {
                var intrinsic = word.LlmAnnotation?.IntrinsicScore ?? LegacyScoreHelper.FromDifficulty(word.DifficultyLevel);
                return (word, intrinsic);
            })
            .Where(item => item.intrinsic >= min && item.intrinsic <= max)
            .OrderBy(_ => Random.Shared.Next())
            .Take(count)
            .ToList();

        var merged = new List<DailyWordItem>();
        foreach (var rel in weak)
        {
            if (rel.Word is null) continue;
            merged.Add(new DailyWordItem(
                rel.Word.Id,
                rel.Word.Lemma,
                rel.Word.Meanings,
                rel.PersonalDifficulty ?? LegacyScoreHelper.FromDifficulty(rel.Word.DifficultyLevel),
                true,
                rel.Word.Phonetics));
        }

        foreach (var (word, intrinsic) in bandWords)
        {
            if (merged.Any(item => item.Id == word.Id)) continue;
            merged.Add(new DailyWordItem(word.Id, word.Lemma, word.Meanings, intrinsic, false, word.Phonetics));
            if (merged.Count >= count) break;
        }

        if (merged.Count == 0)
        {
            merged = candidates.Take(count).Select(word => new DailyWordItem(
                word.Id,
                word.Lemma,
                word.Meanings,
                LegacyScoreHelper.FromDifficulty(word.DifficultyLevel),
                false,
                word.Phonetics)).ToList();
        }

        return merged.Take(count).ToList();
    }
}
