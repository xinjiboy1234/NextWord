namespace NextWord.Domain.Entities;

public sealed class ReadingLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid ArticleId { get; set; }
    public DateTimeOffset StartTime { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndTime { get; set; }
    public int DurationSeconds { get; set; }
    public int LookupCount { get; set; }
    public int CommentsCount { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public User? User { get; set; }
    public Article? Article { get; set; }
}
