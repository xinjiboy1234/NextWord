using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NextWord.Domain.Entities;
using NextWord.Domain.Interfaces;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Background;

/// <summary>
/// 每日为活跃用户写入 ProfileScoreSnapshot，供历史趋势与报告依据。
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
        return created;
    }
}
