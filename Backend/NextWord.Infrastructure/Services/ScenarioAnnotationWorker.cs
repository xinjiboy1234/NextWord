using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NextWord.Domain.Entities;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Domain.Scenarios;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

/// <summary>
/// 场景标注 worker（设计方案 §5）：对未标注（或旧版本标注）的词分批调用 LLM，写入
/// scenarios（0–3 个子场景，0 个 = core 通用桶）、utility、role。
/// 幂等可重跑、断点可续：已标注到当前版本的词自动跳过，重跑只处理剩余词。
/// utility=low 的词按设计不入库——worker 只负责标注，词表准入由种子/导入链路把关。
/// </summary>
public sealed class ScenarioAnnotationWorker(
    ApplicationDbContext db,
    ILLMProvider llm,
    ILogger<ScenarioAnnotationWorker> logger)
{
    public const string JobType = "ScenarioAnnotation";
    public const int CurrentVersion = 1;
    private const int DefaultBatchSize = 20;
    private const int MaxBatchesPerJob = 200;

    public async Task ProcessAsync(BackgroundJob job, CancellationToken cancellationToken)
    {
        var batchSize = ReadBatchSize(job.PayloadJson);

        for (var batchIndex = 0; batchIndex < MaxBatchesPerJob; batchIndex++)
        {
            var batch = await db.Words
                .Include(word => word.Scenarios)
                .Where(word => word.ScenarioAnnotationVersion < CurrentVersion)
                .OrderBy(word => word.Lemma)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
            {
                return;
            }

            var request = new ScenarioAnnotationRequest(
                batch.Select(word => new ScenarioAnnotationItem(word.Lemma, word.PartOfSpeech, word.Meanings)).ToList(),
                new LlmRequestOptions("scenario-annotation-v1", "scenario_annotation"));

            var response = await llm.AnnotateScenarioAsync(request, cancellationToken);
            var results = response.Annotations
                .GroupBy(item => item.Lemma, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var annotated = 0;
            foreach (var word in batch)
            {
                if (!results.TryGetValue(word.Lemma, out var result))
                {
                    // LLM 未返回该词：保持未标注，留给下次重跑续标
                    continue;
                }

                word.Scenarios.RemoveAll(item => !result.ScenarioKeys.Contains(item.ScenarioKey, StringComparer.OrdinalIgnoreCase));
                foreach (var key in result.ScenarioKeys)
                {
                    if (word.Scenarios.All(item => !string.Equals(item.ScenarioKey, key, StringComparison.OrdinalIgnoreCase)))
                    {
                        word.Scenarios.Add(new WordScenario { WordId = word.Id, ScenarioKey = key });
                    }
                }

                word.Utility = result.Utility;
                word.Role = result.Role;
                word.ScenarioAnnotationVersion = CurrentVersion;
                annotated++;
            }

            await db.SaveChangesAsync(cancellationToken);

            if (annotated == 0)
            {
                // 整批无有效结果，防死循环：job 正常结束，剩余词等下次重跑
                logger.LogWarning("Scenario annotation batch made no progress ({Count} words skipped)", batch.Count);
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
