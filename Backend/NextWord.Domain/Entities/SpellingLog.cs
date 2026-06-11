namespace NextWord.Domain.Entities;

public sealed class SpellingLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid WordId { get; set; }
    public string UserSpelling { get; set; } = string.Empty;
    public string CorrectSpelling { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public List<int> ErrorPositions { get; set; } = [];
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public int Attempts { get; set; } = 1;

    public User? User { get; set; }
    public Word? Word { get; set; }
}
