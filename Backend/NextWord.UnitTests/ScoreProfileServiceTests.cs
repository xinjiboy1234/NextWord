using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Models;
using NextWord.Domain.Services;
using NextWord.Infrastructure.Data;
using NextWord.Infrastructure.Services;

namespace NextWord.UnitTests;

public class ScoreProfileServiceTests
{
    private static (ApplicationDbContext Db, ScoreProfileService Service) CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var db = new ApplicationDbContext(options);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        var mapping = new ScoreMappingService(new ScoreMappingOptions());
        var service = new ScoreProfileService(db, mapping);
        return (db, service);
    }

    [Fact]
    public async Task ApplyUpdate_absolute_writes_scores_and_legacy_levels()
    {
        var (db, service) = CreateContext();
        var userId = Guid.NewGuid();
        db.Users.Add(new Domain.Entities.User { Id = userId, DisplayName = "Test" });
        await db.SaveChangesAsync();

        var result = await service.ApplyUpdateAsync(
            new ProfileUpdateCommand(
                userId,
                "AssessmentCompleted",
                new ProfileScoreAssignment(74, 53, 61, 82),
                null,
                "test-absolute-1"),
            CancellationToken.None);

        Assert.True(result.Applied);
        Assert.Equal(53, result.Scores.Overall);
        Assert.Equal("B2", result.Scores.CefrDisplay);

        var progress = await db.UserProgress.SingleAsync(item => item.UserId == userId);
        Assert.Equal(Domain.Enums.CefrLevel.B2, progress.OverallLevel);
        Assert.Equal(1, await db.LearningEvents.CountAsync(item => item.IdempotencyKey == "test-absolute-1"));
    }

    [Fact]
    public async Task ApplyUpdate_is_idempotent()
    {
        var (db, service) = CreateContext();
        var userId = Guid.NewGuid();
        db.Users.Add(new Domain.Entities.User { Id = userId, DisplayName = "Test" });
        await db.SaveChangesAsync();

        var command = new ProfileUpdateCommand(
            userId,
            "ChallengePassed",
            null,
            new ProfileScoreDelta(5, null, null, null),
            "test-idempotent-1");

        var first = await service.ApplyUpdateAsync(command, CancellationToken.None);
        var second = await service.ApplyUpdateAsync(command, CancellationToken.None);

        Assert.True(first.Applied);
        Assert.False(second.Applied);
        Assert.Equal("duplicate", second.SkipReason);
        Assert.Equal(1, await db.LearningEvents.CountAsync(item => item.IdempotencyKey == "test-idempotent-1"));
    }
}
