using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Domain.Scenarios;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

/// <summary>
/// 学习计划服务（T-006，DESIGN-planner-worker §2/§3）：生成 7 日计划并供每日内容消费。
/// 主攻场景只取自最新画像的 Verified 场景 weakness Finding（存疑不进规划）；
/// 画像不足时按场景词覆盖率最低者兜底。同日重复生成幂等（UserId + StartDate 唯一）。
/// </summary>
public sealed class LearningPlanService(
    ApplicationDbContext db,
    IScoreProfileService scoreProfile) : ILearningPlanService
{
    public const int PlanDays = 7;
    public const int DailyWordCount = 10;
    /// <summary>接触词（超带词）占每日词队列的上限（VISION §4.3：≤20%，只进背词识别队列）。</summary>
    public const double MaxExposureRatio = 0.2;
    private const int DailySentenceTargets = 3;
    private const int RecommendedArticleCount = 3;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static DateOnly Today() => DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

    public async Task<LearningPlan> GenerateAsync(Guid userId, CancellationToken cancellationToken, bool force = false)
    {
        var today = Today();
        var existing = await db.LearningPlans
            .Where(plan => plan.UserId == userId && plan.StartDate == today)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null && !force)
        {
            return existing;
        }

        var content = await BuildContentAsync(userId, cancellationToken);
        if (existing is not null)
        {
            // T-007 重规划（瓶颈性质变化 / 每周兜底）：同日已有计划则原地重建内容，保持 (UserId, StartDate) 唯一
            existing.ContentJson = JsonSerializer.Serialize(content, JsonOptions);
            existing.CreatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return existing;
        }

        var plan = new LearningPlan
        {
            UserId = userId,
            StartDate = today,
            ContentJson = JsonSerializer.Serialize(content, JsonOptions),
            ModelProfileId = "planner"
        };
        db.LearningPlans.Add(plan);
        await db.SaveChangesAsync(cancellationToken);
        return plan;
    }

    public async Task<ActiveLearningPlan?> GetActiveAsync(Guid userId, CancellationToken cancellationToken)
    {
        var today = Today();
        var plan = await db.LearningPlans.AsNoTracking()
            .Where(item => item.UserId == userId && item.StartDate <= today)
            .OrderByDescending(item => item.StartDate)
            .FirstOrDefaultAsync(cancellationToken);
        if (plan is null)
        {
            return null;
        }

        var dayIndex = today.DayNumber - plan.StartDate.DayNumber;
        // 过期（>7 天）回退：由消费方走既有难度带逻辑
        if (dayIndex < 0 || dayIndex >= PlanDays)
        {
            return null;
        }

        var content = JsonSerializer.Deserialize<LearningPlanContent>(plan.ContentJson, JsonOptions);
        return content is null ? null : new ActiveLearningPlan(plan, content, dayIndex);
    }

    private async Task<LearningPlanContent> BuildContentAsync(Guid userId, CancellationToken cancellationToken)
    {
        var scores = await scoreProfile.GetScoresAsync(userId, cancellationToken);
        // 水平带用 CEFR（与测评词池口径一致）：词库词多数无 IntrinsicScore 标注，
        // legacy 仅 25/50/75 三档，intrinsic 带会大面积落空
        var userCefr = Enum.TryParse<CefrLevel>(scores.CefrDisplay, out var parsed) ? parsed : CefrLevel.A2;

        // 主攻场景：仅消费 Verified 场景 weakness Finding；画像不足按场景词覆盖率兜底
        var profile = await db.WeaknessProfiles.AsNoTracking()
            .Include(item => item.Findings)
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var verifiedFindings = profile?.Findings
            .Where(finding => finding.Verification == FindingVerification.Verified)
            .ToList() ?? [];

        var focusFindings = verifiedFindings
            .Where(finding => finding.Polarity == FindingPolarity.Weakness
                && finding.Dimension == FindingDimension.Scenario
                && ScenarioTaxonomy.IsSubScenarioKey(finding.DimensionKey))
            .ToList();
        var focusScenarios = focusFindings
            .Select(finding => finding.DimensionKey.ToLowerInvariant())
            .Distinct()
            .Take(2)
            .ToList();
        // 来源标记诚实反映计划消费的 Verified Finding（T-032 修复，QA 验收阻断 2）：
        // 主攻场景仍只取自场景维 weakness Finding，但 Verified 技能维 weakness Finding 同样计入来源——
        // 「个性化」徽章语义 = 计划基于了任何 Verified Finding，不只场景维。
        var sourceFindingIds = focusFindings
            .Where(finding => focusScenarios.Contains(finding.DimensionKey.ToLowerInvariant()))
            .Select(finding => finding.Id)
            .Concat(verifiedFindings
                .Where(finding => finding.Polarity == FindingPolarity.Weakness
                    && finding.Dimension == FindingDimension.Skill)
                .Select(finding => finding.Id))
            .Distinct()
            .ToList();

        if (focusScenarios.Count == 0)
        {
            focusScenarios = await ResolveFallbackScenariosAsync(userId, cancellationToken);
        }

        var days = await BuildDaysAsync(userId, focusScenarios, userCefr, cancellationToken);
        var articleIds = await ResolveArticlesAsync(focusScenarios, userCefr, cancellationToken);

        return new LearningPlanContent(focusScenarios, sourceFindingIds, articleIds, days);
    }

    /// <summary>画像不足兜底：按场景词覆盖率（已学/已标注）最低者取 1–2 个子场景。</summary>
    private async Task<List<string>> ResolveFallbackScenariosAsync(Guid userId, CancellationToken cancellationToken)
    {
        var learnedWordIds = await db.UserWordRelationships.AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => item.WordId)
            .ToListAsync(cancellationToken);
        var learnedSet = learnedWordIds.ToHashSet();

        var links = await db.WordScenarios.AsNoTracking().ToListAsync(cancellationToken);
        var fallback = ScenarioTaxonomy.All
            .Select(sub =>
            {
                var annotated = links.Count(link => string.Equals(link.ScenarioKey, sub.Key, StringComparison.OrdinalIgnoreCase));
                var learned = links.Count(link => string.Equals(link.ScenarioKey, sub.Key, StringComparison.OrdinalIgnoreCase)
                    && learnedSet.Contains(link.WordId));
                return (sub.Key, Annotated: annotated, Coverage: annotated == 0 ? 1.0 : (double)learned / annotated);
            })
            .Where(item => item.Annotated > 0)
            .OrderBy(item => item.Coverage)
            .ThenBy(item => item.Key)
            .Take(2)
            .Select(item => item.Key)
            .ToList();

        return fallback.Count > 0 ? fallback : [ScenarioTaxonomy.All[0].Key];
    }

    /// <summary>
    /// 每日词队列（设计方案 §2）：主攻场景 + core 桶选词，带内（CefrLevel == 用户水平带，
    /// 带池过薄向下一带补充，绝不超带；utility 非 low）为主，每天掺 ≤20% 超带接触词；
    /// 造句目标优先取 T-014 产出候选池（prompted_use 未确认词），T-034 二级补位 Recalled 池
    /// （recalled 且带内、utility 非 low，StageUpdatedAt 最早优先），两级都空才落当日带内词（主攻场景优先）。
    /// </summary>
    private async Task<List<LearningPlanDay>> BuildDaysAsync(
        Guid userId, IReadOnlyList<string> focusScenarios, CefrLevel userCefr, CancellationToken cancellationToken)
    {
        var learnedIds = await db.UserWordRelationships.AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => item.WordId)
            .ToListAsync(cancellationToken);

        var candidates = await db.Words.AsNoTracking()
            .Include(word => word.Scenarios)
            .Where(word => !learnedIds.Contains(word.Id))
            .Where(word => word.Utility != WordUtility.Low)
            .ToListAsync(cancellationToken);

        var pool = candidates
            .Select(word => new
            {
                Word = word,
                InFocus = word.Scenarios.Any(item => focusScenarios.Contains(item.ScenarioKey.ToLowerInvariant()))
            })
            // 主攻场景 + core 通用桶（0 子场景）
            .Where(item => item.InFocus || item.Word.Scenarios.Count == 0)
            .ToList();

        var inBand = pool
            .Where(item => item.Word.CefrLevel == userCefr)
            .ToList();
        // 带池过薄时向下一带补充（与测评词池口径一致），绝不向上超带
        if (inBand.Count < DailyWordCount && userCefr > CefrLevel.A1)
        {
            inBand.AddRange(pool.Where(item => item.Word.CefrLevel == userCefr - 1));
        }

        var orderedInBand = inBand
            .OrderByDescending(item => item.InFocus)
            .ThenBy(_ => Random.Shared.Next())
            .ToList();
        // 接触词：仅超带词（CEFR 严格高于水平带）
        var exposure = pool
            .Where(item => item.Word.CefrLevel > userCefr)
            .OrderByDescending(item => item.InFocus)
            .ThenBy(_ => Random.Shared.Next())
            .ToList();

        var exposurePerDay = (int)(DailyWordCount * MaxExposureRatio);
        var inBandPerDay = DailyWordCount - exposurePerDay;

        // T-014 产出候选池：prompted_use 未确认词优先编入造句目标，7 天顺次消耗
        // T-034 二级补位：prompted_use 池耗尽后接 Recalled 池（最早进阶段的优先），两级都空才落当日带内词
        var candidatePool = await GetPromptedUsePoolAsync(userId, userCefr, cancellationToken);
        var recalledPool = await GetLifecyclePoolAsync(userId, WordLifecycleStage.Recalled, userCefr, cancellationToken);
        var priorityTargets = candidatePool.Concat(recalledPool).ToList();

        var days = new List<LearningPlanDay>();
        for (var day = 0; day < PlanDays; day++)
        {
            var dayWords = orderedInBand.Skip(day * inBandPerDay).Take(inBandPerDay).ToList();
            var dayExposure = exposure.Skip(day * exposurePerDay).Take(exposurePerDay).ToList();
            var targets = priorityTargets.Skip(day * DailySentenceTargets).Take(DailySentenceTargets).ToList();
            if (targets.Count < DailySentenceTargets)
            {
                targets.AddRange(dayWords
                    .OrderByDescending(item => item.InFocus)
                    .Select(item => item.Word.Lemma)
                    .Take(DailySentenceTargets - targets.Count));
            }

            days.Add(new LearningPlanDay(
                dayWords.Select(item => item.Word.Id).ToList(),
                dayExposure.Select(item => item.Word.Id).ToList(),
                targets));
        }

        return days;
    }

    /// <summary>T-014 产出候选池：prompted_use 阶段且未确认的词（带内约束与产出任务口径一致，utility 非 low），按进池时间排序。</summary>
    private async Task<List<string>> GetPromptedUsePoolAsync(Guid userId, CefrLevel userCefr, CancellationToken cancellationToken)
    {
        var relationships = await db.UserWordRelationships.AsNoTracking()
            .Include(item => item.Word)
            .Where(item => item.UserId == userId
                && item.LifecycleStage == WordLifecycleStage.PromptedUse
                && item.PromptedUseConfirmedAt == null)
            .ToListAsync(cancellationToken);
        return FilterInBandLemmas(relationships, userCefr);
    }

    /// <summary>T-034 生命周期阶段词池（Recalled 二级补位用）：带内口径与产出候选池一致（CefrLevel ≤ 用户带、utility 非 low），StageUpdatedAt 最早优先。</summary>
    private async Task<List<string>> GetLifecyclePoolAsync(Guid userId, WordLifecycleStage stage, CefrLevel userCefr, CancellationToken cancellationToken)
    {
        var relationships = await db.UserWordRelationships.AsNoTracking()
            .Include(item => item.Word)
            .Where(item => item.UserId == userId && item.LifecycleStage == stage)
            .ToListAsync(cancellationToken);
        return FilterInBandLemmas(relationships, userCefr);
    }

    private static List<string> FilterInBandLemmas(List<UserWordRelationship> relationships, CefrLevel userCefr) =>
        relationships
            .Where(item => item.Word is not null && item.Word.CefrLevel <= userCefr && item.Word.Utility != WordUtility.Low)
            .OrderBy(item => item.StageUpdatedAt)
            .Select(item => item.Word!.Lemma)
            .Distinct()
            .ToList();

    /// <summary>阅读推荐：主攻场景 TopicTag/场景名匹配优先，难度（CEFR）就近，取前 3 篇。</summary>
    private async Task<List<Guid>> ResolveArticlesAsync(
        IReadOnlyList<string> focusScenarios, CefrLevel userCefr, CancellationToken cancellationToken)
    {
        var focusSet = focusScenarios
            .SelectMany(key =>
            {
                var sub = ScenarioTaxonomy.Find(key);
                return sub is null
                    ? [key]
                    : new[] { sub.Key, sub.ZhName, sub.CategoryKey, sub.CategoryZhName };
            })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var articles = await db.Articles.AsNoTracking().ToListAsync(cancellationToken);
        return articles
            .Select(article => new
            {
                Article = article,
                Match = article.TopicTag is not null && focusSet.Contains(article.TopicTag),
                Distance = Math.Abs((int)article.CefrLevel - (int)userCefr)
            })
            .OrderByDescending(item => item.Match)
            .ThenBy(item => item.Distance)
            .ThenBy(item => item.Article.Title)
            .Take(RecommendedArticleCount)
            .Select(item => item.Article.Id)
            .ToList();
    }
}
