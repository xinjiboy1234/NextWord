namespace NextWord.Domain.Entities;

public sealed class ProfileScoreSnapshot
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public string ScoresJson { get; set; } = "{}";

    public User? User { get; set; }
}
