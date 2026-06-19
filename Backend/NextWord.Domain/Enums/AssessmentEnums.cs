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
    Vocabulary = 1,
    Spelling = 2,
    Sentence = 3,
    Reading = 4,
    FinalLevel = 5
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
