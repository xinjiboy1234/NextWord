using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Infrastructure.Data;
using NextWord.Infrastructure.Services;

namespace NextWord.Infrastructure.Background;

/// <summary>
/// 每日为活跃用户写入 ProfileScoreSnapshot，供历史趋势与报告依据。
/// T-007：快照后跑瓶颈指标筛查（纯规则、零 LLM），触发信号的用户入队 BottleneckInsight 任务
/// （幂等键 insight:{userId}:{yyyyMMdd}，同日至多一次细读）。
/// T-032：日检顺带跑画像冷启动触发判定（纯服务）——满 7 天或产出证据 ≥10 条且从未冷启动重生成
/// → WeaknessProfileService.GenerateAsync(coldStart: true)（放宽档 + 标记位，每用户仅一次）+ 强制 Planner 入队。
/// </summary>
public sealed class ProfileScoreSnapshotWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ProfileScoreSnapshotWorker> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunSnapshotAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Profile score snapshot worker failed.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    internal async Task<int> RunSnapshotAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var scoreProfile = scope.ServiceProvider.GetRequiredService<IScoreProfileService>();
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

        var userIds = await db.UserProgress
            .AsNoTracking()
            .Where(item => item.HasCompletedInitialAssessment)
            .Select(item => item.UserId)
            .ToListAsync(cancellationToken);

        var existing = await db.ProfileScoreSnapshots
            .AsNoTracking()
            .Where(item => item.Date == today && userIds.Contains(item.UserId))
            .Select(item => item.UserId)
            .ToListAsync(cancellationToken);

        var pending = userIds.Except(existing).ToList();
        var created = 0;

        foreach (var userId in pending)
        {
            var scores = await scoreProfile.GetScoresAsync(userId, cancellationToken);
            db.ProfileScoreSnapshots.Add(new ProfileScoreSnapshot
            {
                UserId = userId,
                Date = today,
                ScoresJson = JsonSerializer.Serialize(scores, JsonOptions)
            });
            created += 1;
        }

        if (created > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("Profile score snapshot worker wrote {Count} snapshots for {Date}.", created, today);

        // T-007 指标筛查（规则、零 LLM）：触发信号的用户入队 InsightAgent 细读任务
        var screening = scope.ServiceProvider.GetRequiredService<IBottleneckScreeningService>();
        var backgroundJobs = scope.ServiceProvider.GetRequiredService<IBackgroundJobService>();
        var triggered = 0;
        foreach (var userId in userIds)
        {
            var signals = await screening.ScreenAsync(userId, cancellationToken);
            if (signals.Count == 0)
            {
                continue;
            }

            await backgroundJobs.EnqueueAsync(
                BottleneckInsightWorker.JobType,
                JsonSerializer.Serialize(new { userId, signals = signals.Select(signal => signal.ToWireName()) }, JsonOptions),
                $"insight:{userId}:{today:yyyyMMdd}",
                cancellationToken);
            triggered += 1;
        }

        if (triggered > 0)
        {
            logger.LogInformation("Bottleneck screening triggered insight jobs for {Count} users.", triggered);
        }

        // T-032 画像冷启动重生成（每用户仅一次）：满 7 天或产出证据 ≥10 条 → 放宽档重生成画像 + 强制重规划。
        // 面向全部用户（含跳过首测的用户），不限于上方已完成首测的快照用户集。
        var coldStart = scope.ServiceProvider.GetRequiredService<IColdStartExplorationService>();
        var weaknessProfiles = scope.ServiceProvider.GetRequiredService<IWeaknessProfileService>();
        var allUserIds = await db.Users.AsNoTracking()
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);
        var coldStartTriggered = 0;
        foreach (var userId in allUserIds)
        {
            var evaluation = await coldStart.EvaluateTriggerAsync(userId, cancellationToken);
            if (!evaluation.ShouldTrigger)
            {
                continue;
            }

            await weaknessProfiles.GenerateAsync(userId, null, cancellationToken, coldStart: true);
            await backgroundJobs.EnqueueAsync(
                PlannerWorker.JobType,
                JsonSerializer.Serialize(new { userId, force = true }, JsonOptions),
                $"planner:coldstart:{userId}:{today:yyyyMMdd}",
                cancellationToken);
            coldStartTriggered += 1;
        }

        if (coldStartTriggered > 0)
        {
            logger.LogInformation("Cold-start profile regeneration triggered for {Count} users.", coldStartTriggered);
        }

        return created;
    }
}
