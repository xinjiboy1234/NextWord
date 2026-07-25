using NextWord.Domain.Enums;

namespace NextWord.Domain.Entities;

public sealed class UserWordRelationship
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid WordId { get; set; }
    public double MasteryScore { get; set; }
    public int TimesLearned { get; set; }
    public int TimesCorrect { get; set; }
    public int IntervalDays { get; set; } = 1;
    public double EaseFactor { get; set; } = 2.5;
    public int RepeatCount { get; set; }
    public WordSource Source { get; set; } = WordSource.New;
    public bool IsFavorite { get; set; }
    public DateTimeOffset? LastReviewDate { get; set; }
    public DateTimeOffset NextReviewDue { get; set; } = DateTimeOffset.UtcNow.AddDays(1);

    /// <summary>T-014 四阶段生命周期（认识→回忆→造句使用→自发使用），掌握度由阶段派生。</summary>
    public WordLifecycleStage LifecycleStage { get; set; } = WordLifecycleStage.Recognized;
    /// <summary>T-014：最近一次阶段流转时间。</summary>
    public DateTimeOffset? StageUpdatedAt { get; set; }
    /// <summary>T-014：提示造句中首次正确使用目标词的时间（confirmed 后不再重复编排进候选池）。</summary>
    public DateTimeOffset? PromptedUseConfirmedAt { get; set; }
    /// <summary>T-014 毕业留痕：自发使用所在的 FreeExpressionLog Id。</summary>
    public Guid? GraduatedFreeExpressionLogId { get; set; }

    public double EstimatedKnownRate { get; set; } = 0.5;
    public int? PersonalDifficulty { get; set; }
    public DateTimeOffset? PersonalUpdatedAt { get; set; }

    public User? User { get; set; }
    public Word? Word { get; set; }
}
