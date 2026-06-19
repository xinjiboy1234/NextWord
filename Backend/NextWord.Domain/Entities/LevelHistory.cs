using NextWord.Domain.Enums;

namespace NextWord.Domain.Entities;

public sealed class LevelHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public CefrLevel FromLevel { get; set; }
    public CefrLevel ToLevel { get; set; }
    public LevelChangeReason Reason { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public User? User { get; set; }
}
