using NextWord.Domain.Entities;
using NextWord.Domain.Models;

namespace NextWord.Domain.Interfaces;

/// <summary>
/// 学习计划服务（T-006）：PlannerWorker 一日一次生成 7 日计划（只消费 Verified Finding，
/// 画像不足按场景词覆盖率兜底）；同日重复触发幂等。内容消费方（每日选词/阅读推荐/造句出题）
/// 取当日有效计划，无计划或过期（&gt;7 天）回退既有难度带逻辑。
/// </summary>
public interface ILearningPlanService
{
    /// <summary>为指定用户生成当日起的 7 日计划；同日已有计划则直接返回（幂等）。</summary>
    Task<LearningPlan> GenerateAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>取当日有效计划（含反序列化内容与当日下标）；无计划或已过期返回 null。</summary>
    Task<ActiveLearningPlan?> GetActiveAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>当日有效计划：计划实体 + 内容 + 当日下标（0–6）。</summary>
public sealed record ActiveLearningPlan(LearningPlan Plan, LearningPlanContent Content, int DayIndex);
