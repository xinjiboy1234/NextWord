using NextWord.Domain.Enums;

namespace NextWord.Domain.Entities;

public sealed class WordDifficultyAnnotation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WordId { get; set; }
    public DifficultyLevel DifficultyLevel { get; set; }
    public CefrLevel CefrLevel { get; set; }
    public string Reason { get; set; } = string.Empty;
    public RecommendedAction RecommendedAction { get; set; }
    public double Confidence { get; set; }
    public string ModelProfileId { get; set; } = "local-dev";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Word? Word { get; set; }
}
