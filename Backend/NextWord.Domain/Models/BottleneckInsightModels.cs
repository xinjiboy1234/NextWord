using NextWord.Domain.Enums;

namespace NextWord.Domain.Models;

/// <summary>InsightAgent 可细读的产出原文样本（含可引用的 SentenceLog Id 与四维分）。</summary>
public sealed record ProductionSample(
    Guid Id,
    string TargetWord,
    string Scene,
    string Text,
    int Grammar,
    int Natural,
    int Vocabulary,
    int Relevance,
    IReadOnlyList<string> ErrorTags);

/// <summary>
/// InsightAgent 输入（T-007，DESIGN-bottleneck-insight §2.2）：筛查信号 + 近期产出原文 + 当前 Plan 主攻方向。
/// 原文是判断瓶颈性质的依据——洞察必须贴近真实产出字里行间，不能只看聚合指标。
/// </summary>
public sealed record BottleneckInsightRequest(
    string UserLevel,
    IReadOnlyList<string> Signals,
    IReadOnlyList<ProductionSample> Productions,
    IReadOnlyList<string> PlanFocusScenarios,
    IReadOnlyList<string> PlanSentenceTargets,
    LlmRequestOptions? Options = null);

/// <summary>InsightAgent 产出：瓶颈性质 + 一句中文结论 + 证据引用（SentenceLog id，持久化前机械过滤）。</summary>
public sealed record BottleneckInsightResponse(
    BottleneckNature Nature,
    string Statement,
    IReadOnlyList<Guid> EvidenceLogIds);
