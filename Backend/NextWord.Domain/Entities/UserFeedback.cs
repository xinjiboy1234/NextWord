namespace NextWord.Domain.Entities;

public sealed class UserFeedback
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public string FeedbackType { get; set; } = string.Empty;
    public string TargetWord { get; set; } = string.Empty;
    public string? ContextJson { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User? User { get; set; }
}
