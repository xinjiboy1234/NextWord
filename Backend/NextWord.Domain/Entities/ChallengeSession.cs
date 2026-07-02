namespace NextWord.Domain.Entities;

public sealed class ChallengeSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string PackJson { get; set; } = "{}";
    public bool ConfirmationChallenge { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }

    public User? User { get; set; }
}
