using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

/// <summary>
/// InsightAgent 编排（T-007，DESIGN-bottleneck-insight §2.2/§2.3）：筛查触发后取近期产出原文细读，
/// 持久化 BottleneckInsight（证据引用机械过滤到真实 SentenceLog）。
/// 性质是否变化 = 与上一条洞察比对（Plan 主攻方向由最近一次洞察驱动，两者等价）：
/// 未变 → 仅记录；已变（含首次发现）→ 重生成画像（无测评维度，按日幂等）→ 强制 Planner 入队。
/// 洞察只影响解读与规划，不改任何分数。同日幂等：已有当日洞察直接返回 null（零 LLM）。
/// </summary>
public sealed class BottleneckInsightService(
    ApplicationDbContext db,
    IUserLlmProviderFactory llmFactory,
    IWeaknessProfileService weaknessProfiles,
    IBackgroundJobService backgroundJobs,
    ILogger<BottleneckInsightService> logger) : IBottleneckInsightService
{
    private const int ProductionSampleCount = 20;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<BottleneckInsight?> GenerateAsync(Guid userId, IReadOnlyList<BottleneckSignal> signals, CancellationToken cancellationToken)
    {
        if (signals.Count == 0)
        {
            return null;
        }

        var todayStart = new DateTimeOffset(DateTimeOffset.UtcNow.UtcDateTime.Date, TimeSpan.Zero);
        var existsToday = await db.BottleneckInsights.AsNoTracking()
            .AnyAsync(insight => insight.UserId == userId && insight.CreatedAt >= todayStart, cancellationToken);
        if (existsToday)
        {
            return null;
        }

        var logs = await db.SentenceLogs.AsNoTracking()
            .Where(log => log.UserId == userId)
            .OrderByDescending(log => log.Timestamp)
            .Take(ProductionSampleCount)
            .ToListAsync(cancellationToken);
        var samples = logs
            .Select(log => new ProductionSample(
                log.Id, log.TargetWord, log.Scene, log.UserSentence,
                log.GrammarScore, log.NaturalScore, log.VocabularyScore, log.RelevanceScore,
                log.ErrorTags))
            .ToList();

        var (focusScenarios, sentenceTargets) = await LoadActivePlanDirectionAsync(userId, cancellationToken);
        var level = await db.UserProgress.AsNoTracking()
            .Where(progress => progress.UserId == userId)
            .Select(progress => progress.CefrDisplay)
            .FirstOrDefaultAsync(cancellationToken) ?? "A2";

        var request = new BottleneckInsightRequest(
            level,
            signals.Select(signal => signal.ToWireName()).ToList(),
            samples,
            focusScenarios,
            sentenceTargets,
            new LlmRequestOptions("bottleneck-insight", "bottleneck_insight"));
        var llm = await llmFactory.GetForUserAsync(userId, cancellationToken);
        var response = await llm.GenerateBottleneckInsightAsync(request, cancellationToken);

        // 证据纪律（沿用画像）：只保留指向真实 SentenceLog 的引用，LLM 编造/越权的 id 机械丢弃
        var validIds = logs.Select(log => log.Id).ToHashSet();
        var evidence = response.EvidenceLogIds.Where(validIds.Contains).Distinct().ToList();

        var previous = await db.BottleneckInsights.AsNoTracking()
            .Where(insight => insight.UserId == userId)
            .OrderByDescending(insight => insight.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var natureChanged = previous is null || previous.Nature != response.Nature;

        var insight = new BottleneckInsight
        {
            UserId = userId,
            Nature = response.Nature,
            Signals = string.Join(",", signals.Select(signal => signal.ToWireName())),
            Statement = response.Statement,
            EvidenceJson = JsonSerializer.Serialize(evidence, JsonOptions),
            ReplanTriggered = natureChanged,
            ModelProfileId = request.Options!.ModelProfileId
        };
        db.BottleneckInsights.Add(insight);
        await db.SaveChangesAsync(cancellationToken);

        if (natureChanged)
        {
            // 事件驱动重规划：重生成画像（AssessmentId 空 → 按日幂等）→ 强制 Planner 出新 Plan
            await weaknessProfiles.GenerateAsync(userId, null, cancellationToken);
            await backgroundJobs.EnqueueAsync(
                PlannerWorker.JobType,
                JsonSerializer.Serialize(new { userId, force = true }, JsonOptions),
                $"planner:replan:{userId}:{DateTimeOffset.UtcNow:yyyyMMdd}",
                cancellationToken);
            logger.LogInformation(
                "Bottleneck nature changed for user {UserId}: {Previous} -> {Current}, replan enqueued.",
                userId, previous?.Nature.ToString() ?? "(none)", response.Nature);
        }
        else
        {
            logger.LogInformation("Bottleneck insight recorded for user {UserId}: nature {Nature} unchanged.", userId, response.Nature);
        }

        return insight;
    }

    /// <summary>当前生效 Plan 的主攻方向（主攻场景 + 全部造句目标词）；无生效 Plan 返回空。</summary>
    private async Task<(IReadOnlyList<string> FocusScenarios, IReadOnlyList<string> SentenceTargets)> LoadActivePlanDirectionAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        var plan = await db.LearningPlans.AsNoTracking()
            .Where(item => item.UserId == userId && item.StartDate <= today && item.StartDate >= today.AddDays(-(LearningPlanService.PlanDays - 1)))
            .OrderByDescending(item => item.StartDate)
            .FirstOrDefaultAsync(cancellationToken);
        if (plan is null)
        {
            return ([], []);
        }

        var content = JsonSerializer.Deserialize<LearningPlanContent>(plan.ContentJson, JsonOptions);
        if (content is null)
        {
            return ([], []);
        }

        return (content.FocusScenarios,
            content.Days.SelectMany(day => day.SentenceTargets).Distinct().ToList());
    }
}
