using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Domain.Services;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

/// <summary>
/// 每日选词（T-006 内容来源切换）：优先执行当日 LearningPlan 词队列
/// （带内词 + ≤20% 超带接触词，接触词只进识别队列）；
/// 无 Plan、Plan 过期（&gt;7 天）或当日队列为空 → 回退既有 [score, score+12] 难度带逻辑。
/// T-034：两条路径都保证 ≥40% 名额给「已成熟待推进」老词的回忆考察位，不足时新词补位。
/// </summary>
public sealed class DailyWordSelectionService(
    ApplicationDbContext db,
    IScoreProfileService scoreProfile,
    ILearningPlanService learningPlans) : IDailyWordSelectionService
{
    /// <summary>T-034（DESIGN-lifecycle-acceleration §2.1）：每日词队列回忆考察位比例（保底名额，留常量待仿真校准）。</summary>
    public const double RecallExamQuotaRatio = 0.4;

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

        // T-034：回忆考察位保底（Plan 定词、配额定考察模式）——成熟待推进老词先占 ≥40% 名额，Plan 词补满
        var merged = new List<DailyWordItem>();
        var pool = await GetRecallExamPoolAsync(userId, cancellationToken);
        foreach (var rel in pool)
        {
            if (merged.Count >= RecallExamSlots(count)) break;
            merged.Add(ToReviewItem(rel));
        }

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
        // T-034：已在薄弱复习位的成熟待推进词计入回忆考察配额
        var recallFilled = 0;
        foreach (var rel in weak)
        {
            if (rel.Word is null) continue;
            // T-014：复习词带生命周期阶段与考察模式（认识=看词知义，回忆及以后=看义想词）
            merged.Add(ToReviewItem(rel));
            if (IsMaturePending(rel)) recallFilled++;
        }

        // T-034 回忆考察位：成熟待推进老词保底 ≥40% 名额（去重已在复习位的词），不足时新词补位
        var pool = await GetRecallExamPoolAsync(userId, cancellationToken);
        foreach (var rel in pool)
        {
            if (recallFilled >= RecallExamSlots(count) || merged.Count >= count) break;
            if (merged.Any(item => item.Id == rel.WordId)) continue;
            merged.Add(ToReviewItem(rel));
            recallFilled++;
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

    /// <summary>T-034 回忆考察位名额（≥40%，向上取整保「至少」口径）。</summary>
    private static int RecallExamSlots(int count) => (int)Math.Ceiling(count * RecallExamQuotaRatio);

    /// <summary>
    /// T-034（DESIGN-lifecycle-acceleration §2.1）「已成熟待回忆考察」判定：回忆阶段词
    /// （认识词在 SM-2 成熟那次认识考察即升回忆，成熟老词实际落在本阶段，考察模式天然 recall），
    /// 加上认识且 RepeatCount 达成熟阈值的残留词（答错但自评 Remembered 的边界；其推进考察为认识模式，答对即升回忆）。
    /// </summary>
    private static bool IsMaturePending(UserWordRelationship relationship) =>
        relationship.LifecycleStage == WordLifecycleStage.Recalled
        || (relationship.LifecycleStage == WordLifecycleStage.Recognized
            && relationship.RepeatCount >= WordLifecycleService.MatureRepeatCount);

    /// <summary>T-034 回忆考察池：成熟待推进老词，StageUpdatedAt 最早（最久未推进）优先。考察模式按阶段派生，不动状态机。</summary>
    private async Task<List<UserWordRelationship>> GetRecallExamPoolAsync(Guid userId, CancellationToken cancellationToken)
    {
        var candidates = await db.UserWordRelationships
            .AsNoTracking()
            .Include(item => item.Word)
            .Where(item => item.UserId == userId && item.Word != null
                && (item.LifecycleStage == WordLifecycleStage.Recalled
                    || item.LifecycleStage == WordLifecycleStage.Recognized))
            .ToListAsync(cancellationToken);
        return candidates
            .Where(IsMaturePending)
            .OrderBy(item => item.StageUpdatedAt)
            .ToList();
    }

    /// <summary>复习/考察位词项：带生命周期阶段与考察模式（T-014），T-034 回忆考察池与薄弱复习位共用。</summary>
    private static DailyWordItem ToReviewItem(UserWordRelationship relationship) => new(
        relationship.Word!.Id,
        relationship.Word.Lemma,
        relationship.Word.Meanings,
        relationship.PersonalDifficulty ?? LegacyScoreHelper.FromDifficulty(relationship.Word.DifficultyLevel),
        true,
        relationship.Word.Phonetics,
        Stage: WordLifecycleService.ToToken(relationship.LifecycleStage),
        QuizMode: WordLifecycleService.QuizModeToken(WordLifecycleService.QuizModeForStage(relationship.LifecycleStage)));
}
