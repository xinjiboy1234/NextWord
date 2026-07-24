using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

/// <summary>
/// Profiler Agent（T-005）：聚合库内真实数据（造句留痕 / 测评 FinalLevel / 场景词统计 / 阅读行为）
/// → 调 LLM 产出 Finding 草稿。草稿的真实性由 Verifier 核查，Profiler 本身不做断言。
/// </summary>
public sealed class WeaknessProfiler(
    ApplicationDbContext db,
    IUserLlmProviderFactory llmFactory) : IWeaknessProfiler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<WeaknessProfileResponse> BuildDraftsAsync(Guid userId, Guid? assessmentId, CancellationToken cancellationToken)
    {
        var logs = await db.SentenceLogs.AsNoTracking()
            .Where(log => log.UserId == userId)
            .OrderByDescending(log => log.Timestamp)
            .Take(30)
            .ToListAsync(cancellationToken);
        var logEvidence = logs
            .Select(log => new SentenceLogEvidence(
                log.Id, log.TargetWord, log.Scene,
                log.GrammarScore, log.NaturalScore, log.VocabularyScore, log.RelevanceScore,
                log.ErrorTags))
            .ToList();

        var final = await LoadFinalResultAsync(db, userId, assessmentId, cancellationToken);
        var scenarioStats = await LoadScenarioStatsAsync(userId, cancellationToken);
        var reading = await WeaknessProfileStats.ComputeReadingStatAsync(db, userId, cancellationToken);

        var request = new WeaknessProfileRequest(
            final?.OverallLevel.ToString() ?? "A2",
            final?.Dimensions,
            final?.ExpressionScore,
            logEvidence,
            scenarioStats,
            reading,
            new LlmRequestOptions("weakness-profile", "weakness_profile"));

        var llm = await llmFactory.GetForUserAsync(userId, cancellationToken);
        return await llm.GenerateWeaknessProfileAsync(request, cancellationToken);
    }

    internal static async Task<AssessmentFinalResult?> LoadFinalResultAsync(
        ApplicationDbContext db, Guid userId, Guid? assessmentId, CancellationToken cancellationToken)
    {
        var scoresJson = await db.AssessmentRecords.AsNoTracking()
            .Where(record => record.Step == AssessmentStepType.FinalLevel
                && record.Assessment != null
                && record.Assessment.UserId == userId
                && (assessmentId == null || record.AssessmentId == assessmentId))
            .OrderByDescending(record => record.Timestamp)
            .Select(record => record.ScoresJson)
            .FirstOrDefaultAsync(cancellationToken);

        return scoresJson is null ? null : JsonSerializer.Deserialize<AssessmentFinalResult>(scoresJson, JsonOptions);
    }

    private async Task<IReadOnlyList<ScenarioWordStat>> LoadScenarioStatsAsync(Guid userId, CancellationToken cancellationToken)
    {
        // 只统计用户学过词的场景（无学习行为的场景没有画像意义），按已学词数取前 10
        var learnedWordIds = await db.UserWordRelationships.AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => item.WordId)
            .ToListAsync(cancellationToken);
        if (learnedWordIds.Count == 0)
        {
            return [];
        }

        var scenarioKeys = await db.WordScenarios.AsNoTracking()
            .Where(link => learnedWordIds.Contains(link.WordId))
            .Select(link => link.ScenarioKey)
            .Distinct()
            .ToListAsync(cancellationToken);

        var stats = new List<ScenarioWordStat>();
        foreach (var key in scenarioKeys)
        {
            var stat = await WeaknessProfileStats.ComputeScenarioStatAsync(db, userId, key, cancellationToken);
            if (stat is not null)
            {
                stats.Add(stat);
            }
        }

        return stats
            .OrderByDescending(stat => stat.LearnedWords)
            .Take(10)
            .ToList();
    }
}
