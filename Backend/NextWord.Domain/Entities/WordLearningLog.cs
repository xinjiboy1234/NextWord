using NextWord.Domain.Enums;

namespace NextWord.Domain.Entities;

public sealed class WordLearningLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid WordId { get; set; }
    public string Answer { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public AssessmentResult Rating { get; set; }
    public int ResponseTimeMs { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public User? User { get; set; }
    public Word? Word { get; set; }
}
