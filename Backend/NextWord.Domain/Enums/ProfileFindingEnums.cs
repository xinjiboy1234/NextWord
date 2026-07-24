namespace NextWord.Domain.Enums;

/// <summary>WeaknessProfile Finding 的维度（T-005，DESIGN-weakness-profile §2）。</summary>
public enum FindingDimension
{
    Scenario = 1,
    Skill = 2,
    Reading = 3
}

/// <summary>Finding 的强弱判定。</summary>
public enum FindingPolarity
{
    Strength = 1,
    Weakness = 2,
    Neutral = 3
}

/// <summary>Profiler 声称的置信度（由证据条数支撑，Verifier 机械核查）。</summary>
public enum FindingConfidence
{
    High = 1,
    Medium = 2,
    Low = 3
}

/// <summary>Verifier 核查结论：存疑条目不展示、不进规划输入。</summary>
public enum FindingVerification
{
    Verified = 1,
    Questioned = 2
}
