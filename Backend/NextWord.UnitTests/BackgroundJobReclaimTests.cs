using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Infrastructure.Background;
using NextWord.Infrastructure.Data;

namespace NextWord.UnitTests;

/// <summary>
/// T-013 僵尸任务回收（真实 PG）：超时 Processing 重置回 Pending（带重试计数），
/// 超上限标记 Failed 留痕；未超时与存量空 StartedAt 的边界。
/// </summary>
public class BackgroundJobReclaimTests
{
    [Fact]
    public async Task Stale_processing_job_is_reset_to_pending_for_rerun()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var now = DateTimeOffset.UtcNow;
        var stale = SeedJob("Processing", startedAt: now.AddMinutes(-10));
        var fresh = SeedJob("Processing", startedAt: now.AddSeconds(-30));
        var legacy = SeedJob("Processing", startedAt: null); // 修复前的存量僵尸（无 StartedAt）
        var pending = SeedJob("Pending", startedAt: null);
        db.BackgroundJobs.AddRange(stale, fresh, legacy, pending);
        await db.SaveChangesAsync();

        var reclaimed = await StaleJobReclaimer.ReclaimAsync(db, now, CancellationToken.None);

        Assert.Equal(2, reclaimed);
        Assert.Equal("Pending", stale.Status);
        Assert.Equal(1, stale.RetryCount);
        Assert.Equal("Pending", legacy.Status);
        Assert.Equal(1, legacy.RetryCount);
        // 未超时的 Processing 与正常 Pending 不受影响
        Assert.Equal("Processing", fresh.Status);
        Assert.Equal(0, fresh.RetryCount);
        Assert.Equal("Pending", pending.Status);
        Assert.Equal(0, pending.RetryCount);

        // 回收后任务能被 worker 重新捞到（Pending 查询口径与 worker 一致）
        var rerunnable = await db.BackgroundJobs
            .Where(job => job.Status == "Pending")
            .Select(job => job.Id)
            .ToListAsync();
        Assert.Contains(stale.Id, rerunnable);
    }

    [Fact]
    public async Task Reclaim_beyond_retry_limit_marks_failed_with_trace()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var now = DateTimeOffset.UtcNow;
        var exhausted = SeedJob("Processing", startedAt: now.AddMinutes(-30), retryCount: StaleJobReclaimer.MaxRetryCount);
        var retriable = SeedJob("Processing", startedAt: now.AddMinutes(-30), retryCount: StaleJobReclaimer.MaxRetryCount - 1);
        db.BackgroundJobs.AddRange(exhausted, retriable);
        await db.SaveChangesAsync();

        await StaleJobReclaimer.ReclaimAsync(db, now, CancellationToken.None);

        Assert.Equal("Failed", exhausted.Status);
        Assert.Equal(StaleJobReclaimer.MaxRetryCount + 1, exhausted.RetryCount);
        Assert.NotNull(exhausted.ErrorMessage);
        Assert.NotNull(exhausted.ProcessedAt);

        Assert.Equal("Pending", retriable.Status);
        Assert.Equal(StaleJobReclaimer.MaxRetryCount, retriable.RetryCount);
        Assert.Null(retriable.ProcessedAt);
    }

    private static BackgroundJob SeedJob(string status, DateTimeOffset? startedAt, int retryCount = 0) => new()
    {
        JobType = "EvaluationReport",
        PayloadJson = "{}",
        Status = status,
        IdempotencyKey = $"reclaim-test:{Guid.NewGuid():N}",
        StartedAt = startedAt,
        RetryCount = retryCount
    };
}
