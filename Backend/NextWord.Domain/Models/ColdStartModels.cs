namespace NextWord.Domain.Models;

/// <summary>
/// 探索周状态（T-032）：Day 为 1 起的天序号；RemainingEvidence = max(0, 10 − 产出证据条数)；
/// Active 时携带今日场景表达任务（ScenarioKey/ScenarioName/Prompt）。
/// </summary>
public sealed record ExplorationWeekStatus(
    bool Active,
    int Day,
    int TotalDays,
    int EvidenceCount,
    int RemainingEvidence,
    string? ScenarioKey,
    string? ScenarioName,
    string? Prompt)
{
    public static ExplorationWeekStatus Inactive { get; } = new(false, 0, 0, 0, 0, null, null, null);
}

/// <summary>冷启动画像重生成触发判定（T-032）：满 7 天或产出证据 ≥10 条，且从未做过冷启动重生成。</summary>
public sealed record ColdStartTriggerEvaluation(
    bool ShouldTrigger,
    int DaysSinceRegistration,
    int EvidenceCount);
