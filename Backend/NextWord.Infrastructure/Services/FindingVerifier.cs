using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

/// <summary>
/// Verifier Agent（T-005，DESIGN-weakness-profile §4-2）：对每条 Finding 机械核查，不调用 LLM、不做主观改写——
/// ① 证据引用真实存在且属于该用户；② 引用数值与库内重算值一致；③ 证据条数支撑声称的置信度。
/// 任一不通过即标「存疑」（Questioned），不展示、不进规划输入。
/// </summary>
public sealed class FindingVerifier(ApplicationDbContext db) : IFindingVerifier
{
    public async Task<IReadOnlyList<VerifiedFinding>> VerifyAsync(
        Guid userId,
        Guid? assessmentId,
        IReadOnlyList<ProfileFindingDraft> drafts,
        CancellationToken cancellationToken)
    {
        // 预载被引用的造句留痕（限本人记录；引用他人/不存在的 id 直接查不到 → 存疑）
        var logIds = drafts
            .SelectMany(draft => draft.Evidence)
            .Where(claim => claim.Kind == "sentence_log" && Guid.TryParse(claim.RefId, out _))
            .Select(claim => Guid.Parse(claim.RefId))
            .Distinct()
            .ToList();
        var logs = await db.SentenceLogs.AsNoTracking()
            .Where(log => log.UserId == userId && logIds.Contains(log.Id))
            .ToListAsync(cancellationToken);

        AssessmentFinalResult? final = null;
        if (assessmentId.HasValue)
        {
            final = await WeaknessProfiler.LoadFinalResultAsync(db, userId, assessmentId, cancellationToken);
        }

        var results = new List<VerifiedFinding>();
        foreach (var draft in drafts)
        {
            results.Add(await VerifyOneAsync(userId, draft, logs, final, cancellationToken));
        }

        return results;
    }

    private async Task<VerifiedFinding> VerifyOneAsync(
        Guid userId,
        ProfileFindingDraft draft,
        IReadOnlyList<SentenceLog> logs,
        AssessmentFinalResult? final,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(draft.Statement))
        {
            return Questioned(draft, "结论为空");
        }

        if (draft.Evidence.Count == 0)
        {
            return Questioned(draft, "无证据引用");
        }

        foreach (var claim in draft.Evidence)
        {
            var error = await VerifyClaimAsync(userId, claim, logs, final, cancellationToken);
            if (error is not null)
            {
                return Questioned(draft, error);
            }
        }

        // 样本量支撑置信度：high ≥3 条、medium ≥2 条、low ≥1 条有效证据
        var required = draft.Confidence switch
        {
            FindingConfidence.High => 3,
            FindingConfidence.Medium => 2,
            _ => 1
        };
        if (draft.Evidence.Count < required)
        {
            return Questioned(draft, $"样本量不足：{draft.Confidence.ToString().ToLowerInvariant()} 需 ≥{required} 条证据，实际 {draft.Evidence.Count} 条");
        }

        return new VerifiedFinding(draft, FindingVerification.Verified, string.Empty);
    }

    private async Task<string?> VerifyClaimAsync(
        Guid userId,
        EvidenceClaim claim,
        IReadOnlyList<SentenceLog> logs,
        AssessmentFinalResult? final,
        CancellationToken cancellationToken)
    {
        var metric = claim.Metric?.Trim().ToLowerInvariant();
        claim = claim with { Metric = metric };
        switch (claim.Kind)
        {
            case "sentence_log":
            {
                if (!Guid.TryParse(claim.RefId, out var logId))
                {
                    return $"证据引用格式非法：{claim.RefId}";
                }

                var log = logs.FirstOrDefault(item => item.Id == logId);
                if (log is null)
                {
                    return $"证据不存在或不属于该用户：sentence_log {claim.RefId}";
                }

                if (claim.Metric is null)
                {
                    return null;
                }

                var actual = claim.Metric switch
                {
                    "grammar" => (double?)log.GrammarScore,
                    "natural" => log.NaturalScore,
                    "vocabulary" => log.VocabularyScore,
                    "relevance" => log.RelevanceScore,
                    _ => null
                };
                return CheckValue(claim, actual);
            }
            case "assessment_dimension":
            {
                if (final is null)
                {
                    return "测评 FinalLevel 记录缺失，无法核验";
                }

                var actual = claim.Metric switch
                {
                    "grammar" => (double?)final.Dimensions.Grammar,
                    "natural" => final.Dimensions.Natural,
                    "vocabulary" => final.Dimensions.Vocabulary,
                    "relevance" => final.Dimensions.Relevance,
                    "expressionscore" => final.ExpressionScore,
                    _ => null
                };
                return CheckValue(claim, actual);
            }
            case "word_stats":
            {
                var stat = await WeaknessProfileStats.ComputeScenarioStatAsync(db, userId, claim.RefId, cancellationToken);
                if (stat is null)
                {
                    return $"场景 {claim.RefId} 无词标注数据";
                }

                var actual = claim.Metric switch
                {
                    "coverage" => (double?)stat.Coverage,
                    "avgmastery" => stat.AvgMastery,
                    "correctrate" => stat.CorrectRate,
                    _ => null
                };
                return CheckValue(claim, actual);
            }
            case "reading_stats":
            {
                var stat = await WeaknessProfileStats.ComputeReadingStatAsync(db, userId, cancellationToken);
                var actual = claim.Metric switch
                {
                    "sessioncount" => (double?)stat.SessionCount,
                    "avglookupcount" => stat.AvgLookupCount,
                    _ => null
                };
                return CheckValue(claim, actual);
            }
            default:
                return $"未知证据类型：{claim.Kind}";
        }
    }

    private static string? CheckValue(EvidenceClaim claim, double? actual)
    {
        if (actual is null)
        {
            return $"未知指标：{claim.Metric}";
        }

        if (claim.Value is null)
        {
            return $"引用缺少数值：{claim.Kind}/{claim.Metric}";
        }

        if (!Compare(actual.Value, claim.Op, claim.Value.Value))
        {
            return $"引用数值不属实：{claim.Metric} 实际 {actual.Value}，声称 {claim.Op ?? "="} {claim.Value.Value}";
        }

        return null;
    }

    private static bool Compare(double actual, string? op, double claimed) => op switch
    {
        "<=" => actual <= claimed + 1e-9,
        ">=" => actual >= claimed - 1e-9,
        "<" => actual < claimed,
        ">" => actual > claimed,
        "=" or "==" or null => Math.Abs(actual - claimed) < 0.051,
        _ => false
    };

    private static VerifiedFinding Questioned(ProfileFindingDraft draft, string note) =>
        new(draft, FindingVerification.Questioned, note);
}
