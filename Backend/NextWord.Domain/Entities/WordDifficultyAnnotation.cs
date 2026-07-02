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

    // Score annotation extension (v1)
    public int? IntrinsicScore { get; set; }
    public int Version { get; set; } = 1;
    public bool IsCurrent { get; set; } = true;
    public string? DimensionsJson { get; set; }
    public string? SourcesJson { get; set; }
    public string? PromptVersion { get; set; }
    public int SchemaVersion { get; set; } = 1;

    public Word? Word { get; set; }
}
