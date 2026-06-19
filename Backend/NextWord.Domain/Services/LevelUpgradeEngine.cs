using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;

namespace NextWord.Domain.Services;

/// <summary>
/// 等级升降引擎：连续表现达标触发升级候选，确认挑战失败则回退。
/// </summary>
public sealed class LevelUpgradeEngine : ILevelEngine
{
    public UpgradeCheckResult EvaluateUpgradeCandidate(UserProgress progress, IReadOnlyList<ChallengeRecord> recentChallenges)
    {
        if (progress.IsLevelLocked)
        {
            return new UpgradeCheckResult(false, true, "Level confirmation challenge in progress.");
        }

        var daysAtLevel = progress.LevelStartDate.HasValue
            ? DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - progress.LevelStartDate.Value.DayNumber
            : progress.StreakDays;

        var stablePerformance = progress.StreakDays >= 3 && daysAtLevel >= 3;
        var recentPass = recentChallenges.Any(record => record.Passed && record.Timestamp >= DateTimeOffset.UtcNow.AddDays(-7));

        if (stablePerformance || recentPass)
        {
            return new UpgradeCheckResult(true, true, "User meets upgrade candidate criteria.");
        }

        return new UpgradeCheckResult(false, false, "Upgrade criteria not met.");
    }

    public CefrLevel GetNextLevel(CefrLevel current) =>
        current >= CefrLevel.C1 ? CefrLevel.C1 : (CefrLevel)((int)current + 1);

    public CefrLevel GetPreviousLevel(CefrLevel current) =>
        current <= CefrLevel.A1 ? CefrLevel.A1 : (CefrLevel)((int)current - 1);

    public Task ApplyLevelChangeAsync(Guid userId, CefrLevel from, CefrLevel to, LevelChangeReason reason, CancellationToken cancellationToken)
    {
        // 实际写入由 Infrastructure AssessmentService 完成；此处保留接口契约
        _ = userId;
        _ = from;
        _ = to;
        _ = reason;
        return Task.CompletedTask;
    }
}
