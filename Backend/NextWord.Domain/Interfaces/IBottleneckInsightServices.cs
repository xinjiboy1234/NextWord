using NextWord.Domain.Entities;
using NextWord.Domain.Enums;

namespace NextWord.Domain.Interfaces;

/// <summary>
/// 指标筛查（T-007，DESIGN-bottleneck-insight §2.1）：纯规则、零 LLM，随日快照任务运行。
/// 只判「要不要细看」，不做结论；返回空列表 = 不触发（全程无 LLM 成本）。
/// </summary>
public interface IBottleneckScreeningService
{
    Task<IReadOnlyList<BottleneckSignal>> ScreenAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>
/// InsightAgent 编排（T-007 §2.2/§2.3）：细读产出原文 → 持久化 BottleneckInsight（带证据引用）→
/// 性质已变时触发重规划（重生成画像 + 强制 Planner 入队）；同日幂等（已有当日洞察直接返回 null，零 LLM）。
/// </summary>
public interface IBottleneckInsightService
{
    Task<BottleneckInsight?> GenerateAsync(Guid userId, IReadOnlyList<BottleneckSignal> signals, CancellationToken cancellationToken);
}
