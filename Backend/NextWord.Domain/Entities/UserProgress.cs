using NextWord.Domain.Enums;

namespace NextWord.Domain.Entities;

public sealed class UserProgress
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public CefrLevel OverallLevel { get; set; } = CefrLevel.A1;
    public CefrLevel VocabLevel { get; set; } = CefrLevel.A1;
    public CefrLevel SpellingLevel { get; set; } = CefrLevel.A1;
    public CefrLevel SentenceLevel { get; set; } = CefrLevel.A1;
    public CefrLevel ReadingLevel { get; set; } = CefrLevel.A1;
    public int StreakDays { get; set; }
    public DateOnly? LastStudyDate { get; set; }

    public User? User { get; set; }
}
