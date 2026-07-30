using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Domain.Scenarios;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

/// <summary>
/// 画像冷启动「探索周」（T-032，DESIGN-cold-start-profile §2）：
/// 注册起 7 天内每日按 taxonomy 轮转选 1 个子场景（优先词池已标注场景），出 1 道情境表达题；
/// 证据计数 = SentenceLogs + FreeExpressionLogs（产出证据，阅读/背词不计）。
/// 冷启动重生成「仅一次」标记 = 存在 ModelProfileId 为 <see cref="ColdStartModelProfileId"/> 的画像，
/// 与瓶颈触发（T-007）的画像重生成（ModelProfileId = "weakness-profile"）互不混淆。
/// </summary>
public sealed class ColdStartExplorationService(ApplicationDbContext db) : IColdStartExplorationService
{
    public const int ExplorationDays = 7;
    public const int EvidenceTarget = 10;
    /// <summary>冷启动重生成画像的标记位（WeaknessProfile.ModelProfileId），区别于测评/瓶颈触发的 "weakness-profile"。</summary>
    public const string ColdStartModelProfileId = "weakness-profile-coldstart";

    public async Task<ExplorationWeekStatus> GetExplorationWeekAsync(Guid userId, CancellationToken cancellationToken)
    {
        var createdAt = await LoadRegistrationAsync(userId, cancellationToken);
        if (createdAt is null)
        {
            return ExplorationWeekStatus.Inactive;
        }

        var daysSince = DaysSinceRegistration(createdAt.Value);
        if (daysSince >= ExplorationDays)
        {
            return ExplorationWeekStatus.Inactive;
        }

        var evidenceCount = await CountEvidenceAsync(userId, cancellationToken);
        var remaining = Math.Max(0, EvidenceTarget - evidenceCount);
        var day = Math.Clamp(daysSince + 1, 1, ExplorationDays);
        var scenario = await PickScenarioAsync(day, cancellationToken);
        var prompt = $"情境表达｜{scenario.ZhName}：用 1–2 句英文，描述你在「{scenario.ZhName}」场景下的一次经历或接下来的安排。";
        return new ExplorationWeekStatus(
            true, day, ExplorationDays, evidenceCount, remaining, scenario.Key, scenario.ZhName, prompt);
    }

    public async Task<ColdStartTriggerEvaluation> EvaluateTriggerAsync(Guid userId, CancellationToken cancellationToken)
    {
        var createdAt = await LoadRegistrationAsync(userId, cancellationToken);
        if (createdAt is null)
        {
            return new ColdStartTriggerEvaluation(false, 0, 0);
        }

        var daysSince = DaysSinceRegistration(createdAt.Value);
        var alreadyDone = await db.WeaknessProfiles.AsNoTracking()
            .AnyAsync(profile => profile.UserId == userId && profile.ModelProfileId == ColdStartModelProfileId, cancellationToken);
        if (alreadyDone)
        {
            return new ColdStartTriggerEvaluation(false, daysSince, 0);
        }

        var evidenceCount = await CountEvidenceAsync(userId, cancellationToken);
        var shouldTrigger = daysSince >= ExplorationDays || evidenceCount >= EvidenceTarget;
        return new ColdStartTriggerEvaluation(shouldTrigger, daysSince, evidenceCount);
    }

    private async Task<DateTimeOffset?> LoadRegistrationAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await db.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => (DateTimeOffset?)user.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static int DaysSinceRegistration(DateTimeOffset createdAt)
    {
        return (DateTimeOffset.UtcNow.UtcDateTime.Date - createdAt.UtcDateTime.Date).Days;
    }

    /// <summary>产出证据条数：造句留痕 + 自由表达留痕（均在注册之后产生）。</summary>
    private async Task<int> CountEvidenceAsync(Guid userId, CancellationToken cancellationToken)
    {
        var sentences = await db.SentenceLogs.AsNoTracking()
            .CountAsync(log => log.UserId == userId, cancellationToken);
        var expressions = await db.FreeExpressionLogs.AsNoTracking()
            .CountAsync(log => log.UserId == userId, cancellationToken);
        return sentences + expressions;
    }

    /// <summary>今日任务场景：词池已标注场景的子场景按 taxonomy 顺序轮转（第 day 天取第 (day-1) 个）；无标注词时回退全 taxonomy。</summary>
    private async Task<ScenarioTaxonomy.SubScenario> PickScenarioAsync(int day, CancellationToken cancellationToken)
    {
        var annotatedKeys = await db.WordScenarios.AsNoTracking()
            .Select(link => link.ScenarioKey)
            .Distinct()
            .ToListAsync(cancellationToken);
        var annotated = ScenarioTaxonomy.All
            .Where(sub => annotatedKeys.Contains(sub.Key, StringComparer.OrdinalIgnoreCase))
            .ToList();
        var pool = annotated.Count > 0 ? annotated : ScenarioTaxonomy.All.ToList();
        return pool[(day - 1) % pool.Count];
    }
}
