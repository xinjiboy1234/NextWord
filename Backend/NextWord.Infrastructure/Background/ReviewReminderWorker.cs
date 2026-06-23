using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Background;

/// <summary>
/// 定期汇总各用户待复习数量，写入 UserProgress.PendingReviewCount 供首页展示。
/// </summary>
public sealed class ReviewReminderWorker(IServiceScopeFactory scopeFactory, ILogger<ReviewReminderWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshPendingReviewCountsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Review reminder worker failed.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    internal async Task RefreshPendingReviewCountsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTimeOffset.UtcNow;
        var progressList = await db.UserProgress.ToListAsync(cancellationToken);
        if (progressList.Count == 0)
        {
            return;
        }

        var userIds = progressList.Select(progress => progress.UserId).ToList();
        var relationships = await db.UserWordRelationships
            .AsNoTracking()
            .Where(item => userIds.Contains(item.UserId))
            .ToListAsync(cancellationToken);

        foreach (var progress in progressList)
        {
            progress.PendingReviewCount = relationships.Count(
                item => item.UserId == progress.UserId && item.NextReviewDue <= now);
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Review reminder worker updated {Count} users.", progressList.Count);
    }
}
