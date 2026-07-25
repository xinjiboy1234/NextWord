using Microsoft.EntityFrameworkCore;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Background;

/// <summary>
/// T-013 僵尸任务回收：worker 只捞 Pending，进程中断后 Processing 任务永久卡死（T-007 实测暴露）。
/// 每个 worker 循环把超时 Processing 任务重置回 Pending（RetryCount+1），
/// 超过重试上限标记 Failed 留痕（不静默丢弃）。StartedAt 为空的存量 Processing 一律视为超时回收。
/// </summary>
public static class StaleJobReclaimer
{
    /// <summary>Processing 超过该时长视为僵尸（LLM 任务最长分钟级，留足余量）。</summary>
    public static readonly TimeSpan ProcessingTimeout = TimeSpan.FromMinutes(5);
    /// <summary>回收重试上限：第 MaxRetryCount+1 次回收时标记 Failed。</summary>
    public const int MaxRetryCount = 3;

    public static async Task<int> ReclaimAsync(
        ApplicationDbContext db, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var cutoff = now - ProcessingTimeout;
        var stale = await db.BackgroundJobs
            .Where(job => job.Status == "Processing" && (job.StartedAt == null || job.StartedAt < cutoff))
            .ToListAsync(cancellationToken);
        foreach (var job in stale)
        {
            job.RetryCount += 1;
            if (job.RetryCount > MaxRetryCount)
            {
                job.Status = "Failed";
                job.ErrorMessage = $"僵尸任务回收超过重试上限（{MaxRetryCount} 次），标记失败留痕";
                job.ProcessedAt = now;
            }
            else
            {
                // 重置回 Pending 等待重跑；StartedAt 保留首次时间便于排查
                job.Status = "Pending";
            }
        }

        if (stale.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return stale.Count;
    }
}
