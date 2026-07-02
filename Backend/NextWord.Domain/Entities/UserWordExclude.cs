namespace NextWord.Domain.Entities;

public sealed class UserWordExclude
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public string WordLemma { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User? User { get; set; }
}
