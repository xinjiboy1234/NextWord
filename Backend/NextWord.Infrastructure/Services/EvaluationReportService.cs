using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

public sealed class EvaluationReportService(
    ApplicationDbContext db,
    IScoreProfileService scoreProfile,
    IBackgroundJobService backgroundJobs,
    EvaluationDataAssembler assembler) : IEvaluationReportService
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
        report.Status = "Ready";
        await db.SaveChangesAsync(cancellationToken);
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
