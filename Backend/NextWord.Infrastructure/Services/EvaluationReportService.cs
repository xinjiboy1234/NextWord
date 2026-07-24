using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

public sealed class EvaluationReportService(
    ApplicationDbContext db,
    IScoreProfileService scoreProfile,
    IBackgroundJobService backgroundJobs,
    EvaluationDataAssembler assembler,
    IWeaknessProfileService weaknessProfiles,
    ILogger<EvaluationReportService> logger) : IEvaluationReportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<long> EnqueueForUserAsync(Guid userId, string triggerType, Guid? assessmentId, CancellationToken cancellationToken)
    {
        var scores = await scoreProfile.GetScoresAsync(userId, cancellationToken);
        var payload = JsonSerializer.Serialize(new { userId, triggerType, assessmentId, scores }, JsonOptions);
        var key = assessmentId.HasValue
            ? $"eval:{triggerType}:{assessmentId}"
            : $"eval:{triggerType}:{userId}:{DateTimeOffset.UtcNow:yyyyMMdd}";

        var report = new EvaluationReport
        {
            UserId = userId,
            TriggerType = triggerType,
            AssessmentId = assessmentId,
            InputSnapshotJson = JsonSerializer.Serialize(scores, JsonOptions),
            InputSnapshotHash = key,
            ContentJson = "{}",
            Status = "Pending",
            IdempotencyKey = key
        };
        db.EvaluationReports.Add(report);
        await db.SaveChangesAsync(cancellationToken);

        await backgroundJobs.EnqueueAsync("EvaluationReport", JsonSerializer.Serialize(new { reportId = report.Id }, JsonOptions), key, cancellationToken);
        return report.Id;
    }

    public async Task ProcessJobAsync(BackgroundJob job, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(job.PayloadJson);
        var reportId = doc.RootElement.GetProperty("reportId").GetInt64();
        var report = await db.EvaluationReports.FirstOrDefaultAsync(item => item.Id == reportId, cancellationToken)
            ?? throw new InvalidOperationException("Evaluation report not found.");

        var scores = JsonSerializer.Deserialize<UserProfileScores>(report.InputSnapshotJson, JsonOptions)
            ?? throw new InvalidOperationException("Invalid snapshot.");

        var assembly = await assembler.AssembleAsync(report.UserId, cancellationToken);

        // T-005：测评触发的报告先生成 WeaknessProfile（Profiler → Verifier），
        // 报告内容切换为已验证 Finding 列表；画像失败或全存疑时回退模板文案
        IReadOnlyList<ProfileFinding>? verifiedFindings = null;
        if (report.AssessmentId.HasValue)
        {
            try
            {
                var profile = await weaknessProfiles.GenerateAsync(report.UserId, report.AssessmentId.Value, cancellationToken);
                var verified = profile.Findings
                    .Where(finding => finding.Verification == FindingVerification.Verified)
                    .ToList();
                if (verified.Count > 0)
                {
                    verifiedFindings = verified;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "WeaknessProfile generation failed for report {ReportId}, falling back to template", report.Id);
            }
        }

        var strengths = new List<string>();
        var weaknesses = new List<string>();
        if ((scores.Vocabulary ?? 0) >= (scores.Reading ?? 0))
        {
            strengths.Add($"词汇能力较强（{scores.Vocabulary} 分）");
            weaknesses.Add($"阅读相对薄弱（{scores.Reading} 分）");
        }
        else
        {
            strengths.Add($"阅读能力较好（{scores.Reading} 分）");
            weaknesses.Add($"词汇需要加强（{scores.Vocabulary} 分）");
        }

        if (verifiedFindings is not null)
        {
            report.ContentJson = JsonSerializer.Serialize(BuildProfileContent(verifiedFindings, scores), JsonOptions);
        }
        else
        {
            var content = new
            {
                schemaVersion = 1,
                summary = $"你的综合水平为 Overall {scores.Overall}（{scores.CefrDisplay ?? scores.DifficultyBucket}）。",
                strengths,
                weaknesses,
                recommendations = new[]
                {
                    new { action = "完成今日 10 个新词", module = "word-bank" },
                    new { action = "阅读 1 篇匹配难度文章", module = "reading" },
                    new { action = "复习待复习词汇", module = "review" }
                },
                evidence = new
                {
                    vocabulary = scores.Vocabulary,
                    reading = scores.Reading,
                    writing = scores.Writing,
                    overall = scores.Overall,
                    recentLearningCount = CountArray(assembly.RecentLearning),
                    challengeCount = CountArray(assembly.ChallengeHistory),
                    searchHits = assembly.SearchEvidence.Count
                },
                toolPrefetch = new
                {
                    assembly.RecentLearning,
                    assembly.ChallengeHistory,
                    searchEvidence = assembly.SearchEvidence
                },
                profileSnapshot = scores
            };

            report.ContentJson = JsonSerializer.Serialize(content, JsonOptions);
        }

        report.Status = "Ready";
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// schemaVersion 2 报告内容（T-005）：已验证 Finding 列表为主体；
    /// strengths/weaknesses 由 Finding 派生，兼容旧前端展示。
    /// </summary>
    private static object BuildProfileContent(IReadOnlyList<ProfileFinding> findings, UserProfileScores scores)
    {
        return new
        {
            schemaVersion = 2,
            summary = $"你的综合水平为 Overall {scores.Overall}（{scores.CefrDisplay ?? scores.DifficultyBucket}）。以下为经交叉验证的能力画像。",
            strengths = findings.Where(finding => finding.Polarity == FindingPolarity.Strength).Select(finding => finding.Statement).ToList(),
            weaknesses = findings.Where(finding => finding.Polarity == FindingPolarity.Weakness).Select(finding => finding.Statement).ToList(),
            recommendations = new[]
            {
                new { action = "完成今日 10 个新词", module = "word-bank" },
                new { action = "阅读 1 篇匹配难度文章", module = "reading" },
                new { action = "复习待复习词汇", module = "review" }
            },
            findings = findings.Select(finding => new
            {
                dimension = finding.Dimension.ToString().ToLowerInvariant(),
                dimensionKey = finding.DimensionKey,
                polarity = finding.Polarity.ToString().ToLowerInvariant(),
                statement = finding.Statement,
                confidence = finding.Confidence.ToString().ToLowerInvariant(),
                evidence = JsonSerializer.Deserialize<List<EvidenceClaim>>(finding.EvidenceJson, JsonOptions)
            }).ToList(),
            profileSnapshot = scores
        };
    }

    private static int CountArray(object value)
    {
        if (value is System.Collections.ICollection collection)
        {
            return collection.Count;
        }

        return 0;
    }
}
