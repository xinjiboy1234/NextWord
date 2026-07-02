using NextWord.Domain.Enums;

namespace NextWord.Domain.Entities;

public sealed class UserProgress
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public CefrLevel OverallLevel { get; set; } = CefrLevel.A1;
    public CefrLevel VocabLevel { get; set; } = CefrLevel.A1;
    public CefrLevel SpellingLevel { get; set; } = CefrLevel.A1;
    public CefrLevel SentenceLevel { get; set; } = CefrLevel.A1;
    public CefrLevel ReadingLevel { get; set; } = CefrLevel.A1;
    public int StreakDays { get; set; }
    public DateOnly? LastStudyDate { get; set; }
    public DateOnly? LevelStartDate { get; set; }
    public bool IsLevelLocked { get; set; }
    public bool HasCompletedInitialAssessment { get; set; }
    public int PendingReviewCount { get; set; }
    public bool IsUpgradeCandidate { get; set; }

    // Score kernel (v1) — OverallScore computed at read, not stored
    public int? VocabularyScore { get; set; }
    public int? ReadingScore { get; set; }
    public int? WritingScore { get; set; }
    public int? SpellingScore { get; set; }
    public string? DifficultyBucket { get; set; }
    public string? CefrDisplay { get; set; }
    public DateTimeOffset? ScoresUpdatedAt { get; set; }
    public int ScoreSchemaVersion { get; set; } = 1;
    public string? LegacyCefrJson { get; set; }

    public User? User { get; set; }
}
