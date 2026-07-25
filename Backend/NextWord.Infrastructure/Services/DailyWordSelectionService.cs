using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Domain.Services;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

/// <summary>
/// 每日选词（T-006 内容来源切换）：优先执行当日 LearningPlan 词队列
/// （带内词 + ≤20% 超带接触词，接触词只进识别队列）；
/// 无 Plan、Plan 过期（&gt;7 天）或当日队列为空 → 回退既有 [score, score+12] 难度带逻辑。
/// </summary>
public sealed class DailyWordSelectionService(
    ApplicationDbContext db,
    IScoreProfileService scoreProfile,
    ILearningPlanService learningPlans) : IDailyWordSelectionService
{
    public async Task<IReadOnlyList<DailyWordItem>> GetDailyAsync(Guid userId, int count, CancellationToken cancellationToken)
    {
        count = Math.Clamp(count, 1, 20);

        var planned = await TryGetPlannedAsync(userId, count, cancellationToken);
        if (planned is not null)
        {
            return planned;
        }

        return await GetBandFallbackAsync(userId, count, cancellationToken);
    }

    /// <summary>当日 Plan 词队列；无计划/过期/当日队列为空返回 null 走回退。</summary>
    private async Task<IReadOnlyList<DailyWordItem>?> TryGetPlannedAsync(Guid userId, int count, CancellationToken cancellationToken)
    {
        var active = await learningPlans.GetActiveAsync(userId, cancellationToken);
        if (active is null || active.DayIndex >= active.Content.Days.Count)
        {
            return null;
        }

        var day = active.Content.Days[active.DayIndex];
        if (day.WordIds.Count == 0 && day.ExposureWordIds.Count == 0)
        {
            return null;
        }

        // 接触词占比 ≤20%（Plan 生成已按日限量，这里再兜底一次）
        var exposureCap = (int)(count * LearningPlanService.MaxExposureRatio);
        var wordIds = day.WordIds.Concat(day.ExposureWordIds.Take(exposureCap)).ToList();
        var words = await db.Words.AsNoTracking()
            .Include(word => word.LlmAnnotation)
            .Where(word => wordIds.Contains(word.Id))
            .ToListAsync(cancellationToken);
        var byId = words.ToDictionary(word => word.Id);

        var merged = new List<DailyWordItem>();
        foreach (var id in day.WordIds)
        {
            if (!byId.TryGetValue(id, out var word) || merged.Count >= count) continue;
            merged.Add(ToItem(word, isExposure: false));
        }

        foreach (var id in day.ExposureWordIds.Take(exposureCap))
        {
            if (!byId.TryGetValue(id, out var word) || merged.Count >= count) continue;
            merged.Add(ToItem(word, isExposure: true));
        }

        return merged.Count == 0 ? null : merged;

        static DailyWordItem ToItem(Word word, bool isExposure) => new(
            word.Id,
            word.Lemma,
            word.Meanings,
            word.LlmAnnotation?.IntrinsicScore ?? LegacyScoreHelper.FromDifficulty(word.DifficultyLevel),
            false,
            word.Phonetics,
            true,
            isExposure);
    }

    /// <summary>既有难度带逻辑（VISION §4.3 回退路径）：薄弱词复习 + [score, score+12] 带内新词。</summary>
    private async Task<IReadOnlyList<DailyWordItem>> GetBandFallbackAsync(Guid userId, int count, CancellationToken cancellationToken)
    {
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
            // T-014：复习词带生命周期阶段与考察模式（认识=看词知义，回忆及以后=看义想词）
            merged.Add(new DailyWordItem(
                rel.Word.Id,
                rel.Word.Lemma,
                rel.Word.Meanings,
                rel.PersonalDifficulty ?? LegacyScoreHelper.FromDifficulty(rel.Word.DifficultyLevel),
                true,
                rel.Word.Phonetics,
                Stage: WordLifecycleService.ToToken(rel.LifecycleStage),
                QuizMode: WordLifecycleService.QuizModeToken(WordLifecycleService.QuizModeForStage(rel.LifecycleStage))));
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
