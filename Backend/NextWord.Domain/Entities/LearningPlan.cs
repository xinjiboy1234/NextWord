namespace NextWord.Domain.Entities;

/// <summary>
/// 学习计划（T-006，DESIGN-planner-worker §2）：PlannerWorker 夜间生成，覆盖 StartDate 起 7 日。
/// 内容明细（主攻场景 / 每日词队列 / 阅读推荐 / 造句目标 / 生成依据 Finding id）存 ContentJson，
/// 只引用 Verified Finding；同日重复生成幂等（UserId + StartDate 唯一）。
/// </summary>
public sealed class LearningPlan
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    /// <summary>计划起始日（UTC 日期），覆盖 [StartDate, StartDate+6] 共 7 天。</summary>
    public DateOnly StartDate { get; set; }
    /// <summary>LearningPlanContent JSON。</summary>
    public string ContentJson { get; set; } = "{}";
    public string ModelProfileId { get; set; } = "local-dev";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User? User { get; set; }
}
