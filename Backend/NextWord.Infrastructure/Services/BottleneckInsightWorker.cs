using System.Text.Json;
using Microsoft.Extensions.Logging;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;

namespace NextWord.Infrastructure.Services;

/// <summary>
/// BottleneckInsight 任务处理器（T-007）：由日快照筛查触发入队（幂等键 insight:{userId}:{yyyyMMdd}），
/// 调 InsightAgent 细读产出原文并持久化洞察；性质变化时由服务层触发重规划。
/// </summary>
public sealed class BottleneckInsightWorker(
    IBottleneckInsightService insights,
    ILogger<BottleneckInsightWorker> logger)
{
    public const string JobType = "BottleneckInsight";

    public async Task ProcessAsync(BackgroundJob job, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(job.PayloadJson);
        var userId = doc.RootElement.GetProperty("userId").GetGuid();
        var signals = doc.RootElement.TryGetProperty("signals", out var raw)
            ? raw.EnumerateArray()
                .Select(item => item.GetString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => BottleneckSignalNames.TryParse(value!, out var signal) ? signal : (BottleneckSignal?)null)
                .Where(signal => signal.HasValue)
                .Select(signal => signal!.Value)
                .ToList()
            : [];

        if (signals.Count == 0)
        {
            logger.LogWarning("BottleneckInsight job {JobId} for user {UserId} carries no valid signals, skipped.", job.Id, userId);
            return;
        }

        var insight = await insights.GenerateAsync(userId, signals, cancellationToken);
        logger.LogInformation(
            "Bottleneck insight for user {UserId}: {Result}",
            userId,
            insight is null ? "already generated today (skipped)" : $"{insight.Nature} replan={insight.ReplanTriggered}");
    }
}
