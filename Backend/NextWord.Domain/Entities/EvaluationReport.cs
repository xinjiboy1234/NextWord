namespace NextWord.Domain.Entities;

public sealed class EvaluationReport
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public string TriggerType { get; set; } = string.Empty;
    public Guid? AssessmentId { get; set; }
    public string InputSnapshotJson { get; set; } = "{}";
    public string InputSnapshotHash { get; set; } = string.Empty;
    public string ContentJson { get; set; } = "{}";
    public string Status { get; set; } = "Pending";
    public string IdempotencyKey { get; set; } = string.Empty;
    public string ModelProfileId { get; set; } = "local-dev";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User? User { get; set; }
    public Assessment? Assessment { get; set; }
}
