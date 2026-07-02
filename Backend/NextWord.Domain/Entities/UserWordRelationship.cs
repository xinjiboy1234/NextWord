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

    public double EstimatedKnownRate { get; set; } = 0.5;
    public int? PersonalDifficulty { get; set; }
    public DateTimeOffset? PersonalUpdatedAt { get; set; }

    public User? User { get; set; }
    public Word? Word { get; set; }
}
