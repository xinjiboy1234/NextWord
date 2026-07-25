using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NextWord.Domain.Interfaces;
using NextWord.Infrastructure.Data;
using NextWord.Infrastructure.Services;

namespace NextWord.Infrastructure.Background;

/// <summary>
/// 每周兜底重规划（T-007，DESIGN-bottleneck-insight §2.3）：所有活跃存量用户（完成初测）
/// 每周强制重跑一次 Planner（force，同日已有 Plan 原地重建）——补齐 T-006「无测评用户不获新 Plan」缺口。
/// 每天检查一次，幂等键 planner:weekly:{userId}:{ISO 年}-W{ISO 周}，同周重复入队自动去重。
/// </summary>
public sealed class WeeklyReplanWorker(IServiceScopeFactory scopeFactory, ILogger<WeeklyReplanWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var backgroundJobs = scope.ServiceProvider.GetRequiredService<IBackgroundJobService>();
                var enqueued = await EnqueueWeeklyReplanAsync(db, backgroundJobs, DateTimeOffset.UtcNow, stoppingToken);
                logger.LogInformation("Weekly replan worker enqueued {Count} planner jobs.", enqueued);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Weekly replan worker failed.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    /// <summary>为所有活跃存量用户入队本周强制 Planner 任务；返回新入队数量（同周重复调用幂等）。</summary>
    public static async Task<int> EnqueueWeeklyReplanAsync(
        ApplicationDbContext db,
        IBackgroundJobService backgroundJobs,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var userIds = await db.UserProgress.AsNoTracking()
            .Where(progress => progress.HasCompletedInitialAssessment)
            .Select(progress => progress.UserId)
            .ToListAsync(cancellationToken);

        var week = $"{ISOWeek.GetYear(now.UtcDateTime)}-W{ISOWeek.GetWeekOfYear(now.UtcDateTime):00}";
        var keys = userIds.Select(userId => $"planner:weekly:{userId}:{week}").ToList();
        var existing = (await db.BackgroundJobs.AsNoTracking()
                .Where(job => keys.Contains(job.IdempotencyKey))
                .Select(job => job.IdempotencyKey)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var enqueued = 0;
        foreach (var (userId, key) in userIds.Zip(keys))
        {
            // 幂等键按 ISO 周去重：同周重复运行复用同一 job
            await backgroundJobs.EnqueueAsync(
                PlannerWorker.JobType,
                JsonSerializer.Serialize(new { userId, force = true }),
                key,
                cancellationToken);
            if (!existing.Contains(key))
            {
                enqueued += 1;
            }
        }

        return enqueued;
    }
}
