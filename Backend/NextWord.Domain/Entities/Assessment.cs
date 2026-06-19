using NextWord.Domain.Enums;

namespace NextWord.Domain.Entities;

public sealed class Assessment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public AssessmentType Type { get; set; } = AssessmentType.Initial;
    public AssessmentStatus Status { get; set; } = AssessmentStatus.InProgress;
    public DateTimeOffset StartAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndAt { get; set; }
    public CefrLevel? FinalLevel { get; set; }

    public User? User { get; set; }
    public List<AssessmentRecord> Records { get; set; } = [];
}
