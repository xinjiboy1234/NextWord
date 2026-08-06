using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Models;
using NextWord.Domain.Services;
using NextWord.Infrastructure.Data;
using NextWord.Infrastructure.Services;

namespace NextWord.UnitTests;

/// <summary>
/// T-038：cefrDisplay 跨带抖动迟滞——上行即时、下行需 Overall 连续 3 天低于当前展示档下限
/// （ProfileScoreSnapshots 近 3 天 + 当前值判断；快照不足 3 天不降）。
/// 只影响 CefrDisplay 展示层；OverallLevel 与分数本身不动；测评定级写入（权威锚点）不受迟滞约束。
/// 分带口径（ScoreMapping）：B1 35–70 / B2 70–85。
/// </summary>
public class CefrDisplayHysteresisTests
{
    [Fact]
    public async Task Upgrade_across_band_is_immediate()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var service = new ScoreProfileService(db, new ScoreMappingService(new ScoreMappingOptions()));
        var userId = await SeedUserAtScoreAsync(db, service, 69); // B1 带顶

        var result = await ApplyAbsoluteAsync(service, userId, 70); // 升过 B1 上限 → B2

        Assert.Equal("B2", result.Scores.CefrDisplay);
    }

    [Fact]
    public async Task Single_day_dip_does_not_downgrade()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var service = new ScoreProfileService(db, new ScoreMappingService(new ScoreMappingOptions()));
        var userId = await SeedUserAtScoreAsync(db, service, 72); // B2
        // 近 3 天快照：两天低于 B2 下限 70，昨天仍在带内 → 单日下探不降
        await SeedSnapshotsAsync(db, userId, 68, 69, 71);

        var result = await ApplyAbsoluteAsync(service, userId, 69); // 跌破 70 → raw B1

        Assert.Equal(69, result.Scores.Overall); // overall 确实跌带（sanity）
        Assert.Equal("B2", result.Scores.CefrDisplay); // 展示档保持
        // 只影响展示层：OverallLevel 升级规则不动，仍按 raw 分数映射
        var progress = await db.UserProgress.SingleAsync(item => item.UserId == userId);
        Assert.Equal(CefrLevel.B1, progress.OverallLevel);
        Assert.Equal("B2", progress.CefrDisplay);
    }

    [Fact]
    public async Task Three_consecutive_days_below_floor_downgrades()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var service = new ScoreProfileService(db, new ScoreMappingService(new ScoreMappingOptions()));
        var userId = await SeedUserAtScoreAsync(db, service, 72); // B2
        await SeedSnapshotsAsync(db, userId, 68, 67, 69); // 近 3 天全部低于 B2 下限 70

        var result = await ApplyAbsoluteAsync(service, userId, 69); // 当前值也低于下限

        Assert.Equal("B1", result.Scores.CefrDisplay);
    }

    [Fact]
    public async Task Insufficient_snapshots_do_not_downgrade()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var service = new ScoreProfileService(db, new ScoreMappingService(new ScoreMappingOptions()));
        var userId = await SeedUserAtScoreAsync(db, service, 72); // B2
        await SeedSnapshotsAsync(db, userId, 68, 69); // 只有 2 天快照

        var result = await ApplyAbsoluteAsync(service, userId, 69);

        Assert.Equal("B2", result.Scores.CefrDisplay);
    }

    [Fact]
    public async Task Assessment_write_bypasses_hysteresis()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var service = new ScoreProfileService(db, new ScoreMappingService(new ScoreMappingOptions()));
        var userId = await SeedUserAtScoreAsync(db, service, 72); // B2，无任何快照

        // 测评定级写入（权威锚点，含 T-042 矫正传导的下调）：不受迟滞约束，立即按 raw 映射
        var result = await service.ApplyUpdateAsync(
            new ProfileUpdateCommand(
                userId,
                "AssessmentCompleted",
                new ProfileScoreAssignment(50, 50, 50, null),
                null,
                $"test-t038-bypass-{Guid.NewGuid():N}",
                null,
                BypassCefrDisplayHysteresis: true),
            CancellationToken.None);

        Assert.Equal("B1", result.Scores.CefrDisplay);
    }

    private static async Task<Guid> SeedUserAtScoreAsync(
        ApplicationDbContext db, ScoreProfileService service, int score)
    {
        var userId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, DisplayName = $"t038-{Guid.NewGuid():N}" });
        await db.SaveChangesAsync();

        var initial = await ApplyAbsoluteAsync(service, userId, score);
        Assert.True(initial.Applied);
        return userId;
    }

    private static Task<ProfileUpdateResult> ApplyAbsoluteAsync(ScoreProfileService service, Guid userId, int score) =>
        service.ApplyUpdateAsync(
            new ProfileUpdateCommand(
                userId,
                "Practice",
                new ProfileScoreAssignment(score, score, score, null),
                null,
                $"test-t038-{Guid.NewGuid():N}"),
            CancellationToken.None);

    /// <summary>按「距今由远到近」的顺序写每日快照（ScoresJson 与 ProfileScoreSnapshotWorker 同构，只读 overall 字段）。</summary>
    private static async Task SeedSnapshotsAsync(ApplicationDbContext db, Guid userId, params int[] overalls)
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        for (var index = 0; index < overalls.Length; index++)
        {
            db.ProfileScoreSnapshots.Add(new ProfileScoreSnapshot
            {
                UserId = userId,
                Date = today.AddDays(index - overalls.Length),
                ScoresJson = $"{{\"overall\":{overalls[index]}}}"
            });
        }

        await db.SaveChangesAsync();
    }
}
