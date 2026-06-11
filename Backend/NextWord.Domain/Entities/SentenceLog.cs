using NextWord.Domain.Enums;

namespace NextWord.Domain.Entities;

public sealed class SentenceLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? WordId { get; set; }
    public string TargetWord { get; set; } = string.Empty;
    public string Scene { get; set; } = "life";
    public string UserSentence { get; set; } = string.Empty;
    public string AiRevision { get; set; } = string.Empty;
    public int GrammarScore { get; set; }
    public int NaturalScore { get; set; }
    public int VocabularyScore { get; set; }
    public int RelevanceScore { get; set; }
    public string OverallGrade { get; set; } = "C";
    public List<string> ErrorTags { get; set; } = [];
    public DifficultyLevel DifficultyLevel { get; set; } = DifficultyLevel.Basic;
    public string Suggestion { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public User? User { get; set; }
    public Word? Word { get; set; }
}
