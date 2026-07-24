namespace NextWord.Domain.Enums;

public enum AssessmentType
{
    Initial = 1,
    Challenge = 2
}

public enum AssessmentStatus
{
    InProgress = 1,
    Completed = 2
}

public enum AssessmentStepType
{
    // 1–4 为 T-004 重构前的旧固定步骤，仅为兼容历史记录保留
    Vocabulary = 1,
    Spelling = 2,
    Sentence = 3,
    Reading = 4,
    FinalLevel = 5,
    /// <summary>T-004 起：自适应分块测评的一个块（3–5 题，产出型为主）。</summary>
    AdaptiveBlock = 6
}

/// <summary>自适应测评的带宽移动决策（T-004）。</summary>
public enum BandMove
{
    Down = -1,
    Stay = 0,
    Up = 1
}

public enum LevelChangeReason
{
    Initial = 1,
    Upgrade = 2,
    Rollback = 3
}

public enum ChallengeType
{
    Daily = 1,
    LevelConfirmation = 2
}
