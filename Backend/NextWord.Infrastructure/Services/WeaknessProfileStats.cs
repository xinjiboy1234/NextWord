using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Models;
using NextWord.Domain.Scenarios;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

/// <summary>
/// 画像统计的单一计算来源（T-005）：Profiler 组装提示词与 Verifier 重算核查都走这里，
/// 数值统一保留两位小数，保证「引用值」与「重算值」可机械比对。
/// </summary>
internal static class WeaknessProfileStats
{
    public static async Task<ScenarioWordStat?> ComputeScenarioStatAsync(
        ApplicationDbContext db, Guid userId, string scenarioKey, CancellationToken cancellationToken)
    {
        var wordIds = await db.WordScenarios.AsNoTracking()
            .Where(link => link.ScenarioKey == scenarioKey)
            .Select(link => link.WordId)
            .ToListAsync(cancellationToken);
        if (wordIds.Count == 0)
        {
            return null;
        }

        var relationships = await db.UserWordRelationships.AsNoTracking()
            .Where(item => item.UserId == userId && wordIds.Contains(item.WordId))
            .ToListAsync(cancellationToken);

        var reviewed = relationships.Where(item => item.TimesLearned > 0).ToList();
        var totalReviews = reviewed.Sum(item => item.TimesLearned);
        return new ScenarioWordStat(
            scenarioKey,
            ScenarioTaxonomy.Find(scenarioKey)?.ZhName ?? scenarioKey,
            wordIds.Count,
            relationships.Count,
            Round2((double)relationships.Count / wordIds.Count),
            Round2(relationships.Count == 0 ? 0 : relationships.Average(item => item.MasteryScore)),
            Round2(totalReviews == 0 ? 0 : (double)reviewed.Sum(item => item.TimesCorrect) / totalReviews),
            reviewed.Count);
    }

    public static async Task<ReadingBehaviorStat> ComputeReadingStatAsync(
        ApplicationDbContext db, Guid userId, CancellationToken cancellationToken)
    {
        var logs = await db.ReadingLogs.AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => item.LookupCount)
            .ToListAsync(cancellationToken);
        return new ReadingBehaviorStat(
            logs.Count,
            Round2(logs.Count == 0 ? 0 : logs.Average()));
    }

    public static double Round2(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
