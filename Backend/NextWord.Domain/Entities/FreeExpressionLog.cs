using NextWord.Domain.Enums;

namespace NextWord.Domain.Entities;

public sealed class FreeExpressionLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string UserText { get; set; } = string.Empty;
    public int AiScore { get; set; }
    public string OverallGrade { get; set; } = "C";
    public string AiRevision { get; set; } = string.Empty;
    public List<string> ErrorSentences { get; set; } = [];
    public List<string> Suggestions { get; set; } = [];
    public DifficultyLevel DifficultyLevel { get; set; } = DifficultyLevel.Basic;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public User? User { get; set; }
}
