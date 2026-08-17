using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

/// <summary>
/// T-061 难度标注 worker：对缺少当前 IntrinsicScore 标注的词分批调用 LLM 难度标注，
/// 写入 WordDifficultyAnnotations（IntrinsicScore + CEFR + 难度档），供带内选词细粒度化。
/// 幂等可重跑、断点可续：已标注当前版本（IntrinsicScore 非空）的词自动跳过。
/// Mock/回退结果（ModelProfileId=local-dev）不落库——避免把 mock 占位难度写进词库
/// （词库词本身已带 CEFR，CEFR 六档映射在无真实 LLM 时足够），无 key 环境 job 自然空转。
/// </summary>
public sealed class DifficultyAnnotationWorker(
    ApplicationDbContext db,
    ILLMProvider llm,
    ILogger<DifficultyAnnotationWorker> logger)
{
    public const string JobType = "DifficultyAnnotation";
    /// <summary>真实 LLM 难度标注的 profile 标记（Mock 解析不到该 profile 时回退 local-dev，worker 据此跳过）。</summary>
    public const string ModelProfileId = "difficulty-annotation-v1";
    private const int DefaultBatchSize = 20;
    private const int MaxBatchesPerJob = 200;

    public async Task ProcessAsync(BackgroundJob job, CancellationToken cancellationToken)
    {
        var batchSize = ReadBatchSize(job.PayloadJson);
        var annotated = 0;

        for (var batchIndex = 0; batchIndex < MaxBatchesPerJob; batchIndex++)
        {
            var batch = await db.Words
                .Include(word => word.LlmAnnotation)
                .Where(word => word.LlmAnnotation == null || word.LlmAnnotation.IntrinsicScore == null)
                .OrderBy(word => word.Lemma)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
            {
                return;
            }

            var batchAnnotated = 0;
            foreach (var word in batch)
            {
                var rating = await llm.RateDifficultyAsync(
                    new ItemRatingRequest(ItemType.Word, word.Lemma, new LlmRequestOptions(ModelProfileId, "difficulty_annotation")),
                    cancellationToken);
                // Mock/回退结果不落库（词库词自带 CEFR，CEFR 映射已够用；防 mock 占位污染）
                if (string.Equals(rating.ModelProfileId, "local-dev", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (word.LlmAnnotation is not null)
                {
                    word.LlmAnnotation.IsCurrent = false;
                }

                var annotation = new WordDifficultyAnnotation
                {
                    WordId = word.Id,
                    DifficultyLevel = rating.DifficultyLevel,
                    CefrLevel = rating.CefrLevel,
                    Reason = rating.Reason,
                    RecommendedAction = rating.RecommendedAction,
                    Confidence = Math.Clamp(rating.Confidence, 0, 1),
                    ModelProfileId = rating.ModelProfileId,
                    IntrinsicScore = rating.IntrinsicScore ?? LegacyScoreHelper.FromCefr(rating.CefrLevel),
                    Version = (word.LlmAnnotation?.Version ?? 0) + 1,
                    IsCurrent = true,
                    PromptVersion = "difficulty-annotation-v1",
                    SchemaVersion = 1
                };
                db.WordDifficultyAnnotations.Add(annotation);
                word.LlmAnnotationId = annotation.Id;
                word.LlmAnnotation = annotation;
                word.DifficultyLevel = rating.DifficultyLevel;
                word.CefrLevel = rating.CefrLevel;
                batchAnnotated++;
            }

            if (batchAnnotated > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
                annotated += batchAnnotated;
            }
            else
            {
                // 整批全是 Mock/回退结果（无 key 环境）：不落库、正常结束，下次重跑续标
                logger.LogInformation("Difficulty annotation batch produced no real-LLM results ({Count} words), skipping", batch.Count);
                return;
            }
        }
    }

    private static int ReadBatchSize(string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (doc.RootElement.TryGetProperty("batchSize", out var value) && value.TryGetInt32(out var batchSize))
            {
                return Math.Clamp(batchSize, 1, 50);
            }
        }
        catch (JsonException)
        {
            // 非法 payload 按默认批次处理
        }

        return DefaultBatchSize;
    }
}
