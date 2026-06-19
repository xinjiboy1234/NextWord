using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Services;

namespace NextWord.UnitTests;

public class LevelUpgradeEngineTests
{
    private readonly LevelUpgradeEngine _engine = new();

    [Fact]
    public void Locked_progress_is_not_upgrade_candidate()
    {
        var progress = new UserProgress { IsLevelLocked = true, StreakDays = 10 };
        var result = _engine.EvaluateUpgradeCandidate(progress, []);
        Assert.False(result.IsCandidate);
    }

    [Fact]
    public void Stable_streak_marks_upgrade_candidate()
    {
        var progress = new UserProgress { StreakDays = 3, LevelStartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-4)) };
        var result = _engine.EvaluateUpgradeCandidate(progress, []);
        Assert.True(result.IsCandidate);
    }

    [Fact]
    public void GetNextLevel_stops_at_C1()
    {
        Assert.Equal(CefrLevel.C1, _engine.GetNextLevel(CefrLevel.C1));
        Assert.Equal(CefrLevel.B2, _engine.GetNextLevel(CefrLevel.B1));
    }
}
