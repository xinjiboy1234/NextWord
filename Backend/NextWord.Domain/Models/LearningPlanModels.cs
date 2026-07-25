namespace NextWord.Domain.Models;

/// <summary>
/// LearningPlan 内容（T-006，DESIGN-planner-worker §2）：7 日计划明细。
/// FocusScenarios = 主攻子场景（1–2 个）；SourceFindingIds = 生成依据（仅 Verified Finding id）；
/// ArticleIds = 阅读推荐（按主攻场景选文）；Days = 每日词队列 + 造句目标。
/// </summary>
public sealed record LearningPlanContent(
    IReadOnlyList<string> FocusScenarios,
    IReadOnlyList<long> SourceFindingIds,
    IReadOnlyList<Guid> ArticleIds,
    IReadOnlyList<LearningPlanDay> Days);

/// <summary>
/// 单日计划：WordIds 为水平带内词；ExposureWordIds 为超带「接触词」（≤20%，只进背词识别队列）；
/// SentenceTargets 为造句目标词（带内、主攻场景优先）。
/// </summary>
public sealed record LearningPlanDay(
    IReadOnlyList<Guid> WordIds,
    IReadOnlyList<Guid> ExposureWordIds,
    IReadOnlyList<string> SentenceTargets);
