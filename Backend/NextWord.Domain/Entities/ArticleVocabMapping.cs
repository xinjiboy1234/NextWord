using NextWord.Domain.Enums;

namespace NextWord.Domain.Entities;

public sealed class ArticleVocabMapping
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ArticleId { get; set; }
    public Guid? WordId { get; set; }
    public string WordLemma { get; set; } = string.Empty;
    public string ContextMeaning { get; set; } = string.Empty;
    public string SpecialUsage { get; set; } = string.Empty;
    public DifficultyLevel DifficultyInContext { get; set; } = DifficultyLevel.Basic;
    public RecommendedAction RecommendedAction { get; set; } = RecommendedAction.LearnNow;
    public bool IsKeyVocab { get; set; } = true;

    public Article? Article { get; set; }
    public Word? Word { get; set; }
}
