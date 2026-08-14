using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Domain.Services;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

public sealed class SpellingService(
    ApplicationDbContext db,
    ISm2Service sm2,
    IScoreProfileService scoreProfile,
    IReviewQueueService reviews) : ISpellingService
{
    /// <summary>
    /// T-052：拼写队列组装。Review=只到期复习词；New=只带内新词；Mixed=新词 30%、复习 70%（3:7 取整，
    /// 复习占满剩余名额），复习不足新词补位、新词不足复习补位，两者皆空返回空队列（前端空态）。
    /// </summary>
    public async Task<IReadOnlyList<SpellingQueueItem>> GetQueueAsync(Guid userId, int count, SpellingQueueMode mode, CancellationToken cancellationToken)
    {
        count = Math.Clamp(count, 1, 20);
        var newTarget = mode switch
        {
            SpellingQueueMode.New => count,
            SpellingQueueMode.Review => 0,
            _ => NewSlots(count),
        };

        IReadOnlyList<UserWordRelationship> dueReviews = mode == SpellingQueueMode.New
            ? []
            : await reviews.GetDueReviewsAsync(userId, count, cancellationToken);
        IReadOnlyList<Word> bandNewWords = mode == SpellingQueueMode.Review
            ? []
            : await GetBandNewWordsAsync(userId, count, cancellationToken);

        var pickedNew = bandNewWords.Take(newTarget).ToList();
        var pickedReviews = dueReviews.Take(count - pickedNew.Count).ToList();
        // 一侧不足时另一侧补位，尽量凑满 count
        if (pickedNew.Count + pickedReviews.Count < count)
        {
            pickedNew.AddRange(bandNewWords.Skip(pickedNew.Count).Take(count - pickedNew.Count - pickedReviews.Count));
            pickedReviews.AddRange(dueReviews.Skip(pickedReviews.Count).Take(count - pickedNew.Count - pickedReviews.Count));
        }

        var queue = new List<SpellingQueueItem>(pickedNew.Count + pickedReviews.Count);
        queue.AddRange(pickedReviews
            .Where(item => item.Word is not null)
            .Select(item => new SpellingQueueItem(item.Word!, true)));
        queue.AddRange(pickedNew.Select(word => new SpellingQueueItem(word, false)));
        return queue;
    }

    /// <summary>T-052 mixed 模式新词名额（新旧 3:7，AwayFromZero 取整：count=12 → 新 4 复习 8）。</summary>
    private static int NewSlots(int count) => (int)Math.Round(count * 0.3, MidpointRounding.AwayFromZero);

    /// <summary>
    /// T-052 带内新词：与每日词回退口径（DailyWordSelectionService.GetBandFallbackAsync）一致——
    /// 未学词按内在难度分（LLM 标注分，无标注回退难度档映射分）落在 [vocabScore, vocabScore+12] 带内，随机取。
    /// </summary>
    private async Task<IReadOnlyList<Word>> GetBandNewWordsAsync(Guid userId, int count, CancellationToken cancellationToken)
    {
        var scores = await scoreProfile.GetScoresAsync(userId, cancellationToken);
        var vocabScore = scores.Vocabulary ?? 42;
        var min = vocabScore;
        var max = Math.Min(100, vocabScore + 12);

        var learnedIds = await db.UserWordRelationships
            .Where(item => item.UserId == userId)
            .Select(item => item.WordId)
            .ToListAsync(cancellationToken);

        var candidates = await db.Words.AsNoTracking()
            .Include(word => word.LlmAnnotation)
            .Where(word => !learnedIds.Contains(word.Id))
            .ToListAsync(cancellationToken);

        return candidates
            .Select(word => (word, intrinsic: word.LlmAnnotation?.IntrinsicScore ?? LegacyScoreHelper.FromDifficulty(word.DifficultyLevel)))
            .Where(item => item.intrinsic >= min && item.intrinsic <= max)
            .OrderBy(_ => Random.Shared.Next())
            .Take(count)
            .Select(item => item.word)
            .ToList();
    }

    public async Task<SpellingLog> SubmitAsync(Guid userId, Guid wordId, string userSpelling, int attempts, CancellationToken cancellationToken)
    {
        var word = await db.Words.FirstOrDefaultAsync(item => item.Id == wordId, cancellationToken)
            ?? throw new InvalidOperationException("Word not found.");

        var normalizedInput = Normalize(userSpelling);
        var normalizedCorrect = Normalize(word.Lemma);
        var isCorrect = string.Equals(normalizedInput, normalizedCorrect, StringComparison.OrdinalIgnoreCase);
        var log = new SpellingLog
        {
            UserId = userId,
            WordId = word.Id,
            UserSpelling = userSpelling.Trim(),
            CorrectSpelling = word.Lemma,
            IsCorrect = isCorrect,
            ErrorPositions = FindErrorPositions(normalizedInput, normalizedCorrect),
            Attempts = Math.Max(1, attempts)
        };

        var relationship = await db.UserWordRelationships
            .FirstOrDefaultAsync(item => item.UserId == userId && item.WordId == word.Id, cancellationToken);
        if (relationship is null)
        {
            relationship = new UserWordRelationship
            {
                UserId = userId,
                WordId = word.Id,
                Source = WordSource.New
            };
            db.UserWordRelationships.Add(relationship);
        }

        relationship.TimesLearned += 1;
        relationship.TimesCorrect += isCorrect ? 1 : 0;
        sm2.ApplyReview(relationship, isCorrect ? AssessmentResult.Remembered : AssessmentResult.Forgot, DateTimeOffset.UtcNow);
        // T-014：掌握度阶段派生（不再按结果直接加减）；拼写正确 = 回忆级产出证据，recalled 阶段词进候选池
        WordLifecycleService.ApplyReview(relationship, WordQuizMode.Recall, isCorrect, DateTimeOffset.UtcNow);

        if (!isCorrect && await HasPreviousSpellingMissAsync(userId, word.Id, cancellationToken))
        {
            relationship.NextReviewDue = DateTimeOffset.UtcNow;
            relationship.EaseFactor = Math.Max(1.3, relationship.EaseFactor - 0.15);
        }

        db.SpellingLogs.Add(log);
        await db.SaveChangesAsync(cancellationToken);
        return log;
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();

    private static List<int> FindErrorPositions(string input, string correct)
    {
        var positions = new List<int>();
        var max = Math.Max(input.Length, correct.Length);
        for (var index = 0; index < max; index += 1)
        {
            var inputChar = index < input.Length ? input[index] : '\0';
            var correctChar = index < correct.Length ? correct[index] : '\0';
            if (inputChar != correctChar)
            {
                positions.Add(index);
            }
        }

        return positions;
    }

    private async Task<bool> HasPreviousSpellingMissAsync(Guid userId, Guid wordId, CancellationToken cancellationToken)
    {
        var latest = (await db.SpellingLogs
            .Where(log => log.UserId == userId && log.WordId == wordId)
            .ToListAsync(cancellationToken))
            .OrderByDescending(log => log.Timestamp)
            .FirstOrDefault();

        return latest is not null && !latest.IsCorrect;
    }
}
