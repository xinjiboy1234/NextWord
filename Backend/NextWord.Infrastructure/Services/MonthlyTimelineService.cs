using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Enums;
using NextWord.Domain.Models;
using NextWord.Domain.Services;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

/// <summary>
/// 「我的这个月」聚合（T-036，DESIGN-monthly-timeline §2）：只读拼装月度里程碑事件
/// （词毕业 / 挑战首过 / 定级升级 / 画像生成）+ 画像变化（规则 diff）+ 最近洞察回放。
/// 固定查询数（≤7 次往返），无 N+1，不写任何状态。
/// </summary>
public sealed class MonthlyTimelineService(ApplicationDbContext db)
{
    public async Task<MonthlyTimelineResult> GetAsync(Guid userId, int days, CancellationToken cancellationToken)
    {
        var since = DateTimeOffset.UtcNow.AddDays(-days);

        // 词毕业：自发使用留痕 + 阶段流转时间落窗内（Join Words 取词形，单次查询）
        var graduations = await db.UserWordRelationships.AsNoTracking()
            .Where(item => item.UserId == userId
                && item.GraduatedFreeExpressionLogId != null
                && item.StageUpdatedAt != null
                && item.StageUpdatedAt >= since)
            .Select(item => new { OccurredAt = item.StageUpdatedAt!.Value, Lemma = item.Word!.Lemma })
            .ToListAsync(cancellationToken);

        // 挑战首过：全量通过记录按 AttemptedLevel 取最早一次，最早一次落窗内才是「本月首过」（与 LevelDashboard 口径一致）
        var firstPasses = (await db.ChallengeRecords.AsNoTracking()
                .Where(item => item.UserId == userId && item.Passed)
                .Select(item => new { item.AttemptedLevel, item.Timestamp })
                .ToListAsync(cancellationToken))
            .GroupBy(item => item.AttemptedLevel)
            .Select(group => (Level: group.Key, First: group.Min(item => item.Timestamp)))
            .Where(item => item.First >= since)
            .ToList();

        var levelChanges = await db.LevelHistories.AsNoTracking()
            .Where(item => item.UserId == userId && item.Timestamp >= since)
            .ToListAsync(cancellationToken);

        // 画像：窗口内生成事件（轻投影）+ 最新两份（含 Finding 做 diff）
        var profileEvents = await db.WeaknessProfiles.AsNoTracking()
            .Where(item => item.UserId == userId && item.CreatedAt >= since)
            .Select(item => new { item.Id, item.CreatedAt })
            .ToListAsync(cancellationToken);
        var latestProfiles = await db.WeaknessProfiles.AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(2)
            .Include(item => item.Findings)
            .ToListAsync(cancellationToken);

        var insights = await db.BottleneckInsights.AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(3)
            .ToListAsync(cancellationToken);

        var events = new List<MonthlyTimelineEvent>();
        events.AddRange(graduations.Select(item => new MonthlyTimelineEvent(
            MonthlyTimelineEventTypes.WordGraduation, item.OccurredAt, Word: item.Lemma)));
        events.AddRange(firstPasses.Select(item => new MonthlyTimelineEvent(
            MonthlyTimelineEventTypes.ChallengeFirstPass, item.First, Level: item.Level.ToString())));
        events.AddRange(levelChanges.Select(item => new MonthlyTimelineEvent(
            MonthlyTimelineEventTypes.LevelChange, item.Timestamp,
            FromLevel: item.FromLevel.ToString(), ToLevel: item.ToLevel.ToString(), Reason: item.Reason.ToString())));
        events.AddRange(profileEvents.Select(item => new MonthlyTimelineEvent(
            MonthlyTimelineEventTypes.ProfileGenerated, item.CreatedAt)));
        var ordered = events.OrderByDescending(item => item.OccurredAt).ToList();

        var current = latestProfiles.FirstOrDefault();
        var previous = latestProfiles.Skip(1).FirstOrDefault();
        var diff = current is not null && previous is not null
            ? ProfileChangeDiffer.Diff(previous.Findings, current.Findings)
            : new ProfileChangeDiff([], []);
        var currentFindings = current?.Findings
            .Where(item => item.Verification != FindingVerification.Questioned)
            .Select(item => new ProfileFindingSummaryItem(item.Dimension, item.DimensionKey, item.Polarity, item.Statement))
            .ToList() ?? (IReadOnlyList<ProfileFindingSummaryItem>)[];

        return new MonthlyTimelineResult(
            days,
            ordered,
            new MonthlyProfileChange(
                current is not null,
                previous is not null,
                current?.CreatedAt,
                diff.NewStrengths,
                diff.ImprovedWeaknesses,
                currentFindings),
            insights
                .Select(item => new MonthlyInsightItem(item.Nature.ToString(), item.Statement, item.CreatedAt))
                .ToList());
    }
}
