using NextWord.Domain.Enums;

namespace NextWord.Domain.Entities;

public sealed class ChallengeRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public ChallengeType ChallengeType { get; set; } = ChallengeType.Daily;
    public double VocabularyScore { get; set; }
    public double SentenceScore { get; set; }
    public double ReadingScore { get; set; }
    public double TotalScore { get; set; }
    public bool Passed { get; set; }
    public CefrLevel AttemptedLevel { get; set; } = CefrLevel.A1;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public User? User { get; set; }
}
