using System.Text.Json;
using Microsoft.Extensions.Logging;
using NextWord.Domain.Entities;
using NextWord.Domain.Interfaces;

namespace NextWord.Infrastructure.Services;

/// <summary>
/// PlannerWorker（T-006，DESIGN-planner-worker §3）：一日一次，为指定用户生成 7 日 LearningPlan。
/// 由评估报告任务（测评完成、画像已生成）触发入队，幂等键 planner:{userId}:{yyyyMMdd}，
/// 同日重复触发复用同一 job 且 LearningPlanService 同日不重复生成。
/// T-007：payload 带 force=true 时为重规划（瓶颈性质变化 / 每周兜底），同日已有 Plan 原地重建。
/// </summary>
public sealed class PlannerWorker(
    ILearningPlanService plans,
    ILogger<PlannerWorker> logger)
{
    public const string JobType = "Planner";

    public async Task ProcessAsync(BackgroundJob job, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(job.PayloadJson);
        var userId = doc.RootElement.GetProperty("userId").GetGuid();
        var force = doc.RootElement.TryGetProperty("force", out var rawForce) && rawForce.GetBoolean();

        var plan = await plans.GenerateAsync(userId, cancellationToken, force);
        logger.LogInformation("Learning plan ready for user {UserId}: start {StartDate} (force={Force})", userId, plan.StartDate, force);
    }
}
