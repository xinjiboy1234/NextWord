using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NextWord.Domain.Interfaces;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Background;

/// <summary>
/// 每日检查用户是否满足升级候选条件，结果写入 UserProgress.IsUpgradeCandidate。
/// </summary>
public sealed class LevelCheckWorker(IServiceScopeFactory scopeFactory, ILogger<LevelCheckWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 启动后先跑一次，便于开发验证
        await RunLevelCheckAsync(stoppingToken);

        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunLevelCheckAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Level check worker failed.");
            }
        }
    }

    internal async Task RunLevelCheckAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var levelEngine = scope.ServiceProvider.GetRequiredService<ILevelEngine>();
        var progressList = await db.UserProgress.ToListAsync(cancellationToken);

        foreach (var progress in progressList)
        {
            var recentChallenges = await db.ChallengeRecords.AsNoTracking()
                .Where(record => record.UserId == progress.UserId)
                .OrderByDescending(record => record.Timestamp)
                .Take(5)
                .ToListAsync(cancellationToken);

            var check = levelEngine.EvaluateUpgradeCandidate(progress, recentChallenges);
            progress.IsUpgradeCandidate = check.IsCandidate;
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Level check worker updated {Count} users.", progressList.Count);
    }
}
