using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

/// <summary>
/// Profiler Agent（T-005）：聚合库内真实数据（造句留痕 / 自由表达留痕 / 测评 FinalLevel / 场景词统计 / 阅读行为）
/// → 调 LLM 产出 Finding 草稿。草稿的真实性由 Verifier 核查，Profiler 本身不做断言。
/// T-010：草稿先经 <see cref="Deduplicate"/> 语义去重（同维度至多一条、证据不跨条复用），再交 Verifier。
/// T-032：聚合 FreeExpressionLogs（最近 30 条，与 SentenceLogs 同权作为产出证据）——
/// 探索周主推的自由表达任务产生的证据必须对画像可见（QA 验收阻断 1 修复）。
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

        // T-032：自由表达留痕同权聚合（探索周表达任务攒的证据）
        var freeLogs = await db.FreeExpressionLogs.AsNoTracking()
            .Where(log => log.UserId == userId)
            .OrderByDescending(log => log.Timestamp)
            .Take(30)
            .ToListAsync(cancellationToken);
        var freeEvidence = freeLogs
            .Select(log => new FreeExpressionLogEvidence(log.Id, log.AiScore, log.OverallGrade))
            .ToList();

        var final = await LoadFinalResultAsync(db, userId, assessmentId, cancellationToken);
        var scenarioStats = await LoadScenarioStatsAsync(userId, cancellationToken);
        var reading = await WeaknessProfileStats.ComputeReadingStatAsync(db, userId, cancellationToken);

        var request = new WeaknessProfileRequest(
            final?.OverallLevel.ToString() ?? "A2",
            final?.Dimensions,
            final?.ExpressionScore,
            logEvidence,
            freeEvidence,
            scenarioStats,
            reading,
            new LlmRequestOptions("weakness-profile", "weakness_profile"));

        var llm = await llmFactory.GetForUserAsync(userId, cancellationToken);
        var response = await llm.GenerateWeaknessProfileAsync(request, cancellationToken);
        return new WeaknessProfileResponse(Deduplicate(response.Findings));
    }

    /// <summary>
    /// T-010 后处理去重（语义去重是 Profiler 职责，Verifier 不变）：
    /// 1) 同维度（Dimension + DimensionKey）多条只保留证据更强者（证据条数多者优先，并列取置信度高者）；
    /// 2) 同一证据引用（Kind + RefId + Metric）被多条 Finding 复用时，只留在置信度最高者
    ///    （并列取证据条数多者）；被剥夺后无任何证据的 Finding 整条丢弃。
    /// </summary>
    public static IReadOnlyList<ProfileFindingDraft> Deduplicate(IReadOnlyList<ProfileFindingDraft> drafts)
    {
        // 同维度去重
        var perDimension = drafts
            .GroupBy(draft => (draft.Dimension, Key: draft.DimensionKey.Trim().ToLowerInvariant()))
            .Select(group => group
                .OrderByDescending(draft => draft.Evidence.Count)
                .ThenBy(draft => draft.Confidence)
                .First())
            .ToList();

        // 证据复用去重：按置信度高 → 证据多的顺序占位
        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ProfileFindingDraft>();
        foreach (var draft in perDimension
            .OrderBy(draft => draft.Confidence)
            .ThenByDescending(draft => draft.Evidence.Count))
        {
            var owned = draft.Evidence
                .Where(claim => claimed.Add(EvidenceKey(claim)))
                .ToList();
            if (owned.Count == 0)
            {
                continue;
            }

            result.Add(owned.Count == draft.Evidence.Count ? draft : draft with { Evidence = owned });
        }

        return result;
    }

    private static string EvidenceKey(EvidenceClaim claim)
        => $"{claim.Kind}|{claim.RefId}|{claim.Metric ?? string.Empty}";

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
