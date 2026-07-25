namespace NextWord.Domain.Enums;

/// <summary>
/// T-014 词毕业四阶段生命周期（DESIGN-word-lifecycle §2，枚举存字符串）：
/// 认识 → 回忆 → 造句使用（产出候选池）→ 自发使用（毕业）。
/// 认识/回忆阶段不回退（SM-2 管遗忘调度）；造句使用阶段产出证据显示不会用才退回回忆。
/// </summary>
public enum WordLifecycleStage
{
    /// <summary>认识：看词知义（默认初始阶段）。</summary>
    Recognized,
    /// <summary>回忆：看义想词（SM-2 成熟后进入）。</summary>
    Recalled,
    /// <summary>造句使用：回忆考察通过，进入产出候选池，由 Planner 优先编排造句目标。</summary>
    PromptedUse,
    /// <summary>自发使用（毕业）：自由表达中自发正确使用一次且当次评分达标。</summary>
    SpontaneousUse
}
