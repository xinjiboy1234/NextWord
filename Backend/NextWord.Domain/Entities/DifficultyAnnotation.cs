using NextWord.Domain.Enums;

namespace NextWord.Domain.Entities;

public sealed class DifficultyAnnotation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ItemType ItemType { get; set; }
    public string ItemHash { get; set; } = string.Empty;
    public DifficultyLevel DifficultyLevel { get; set; }
    public CefrLevel CefrLevel { get; set; }
    public string Reason { get; set; } = string.Empty;
    public RecommendedAction RecommendedAction { get; set; }
    public double Confidence { get; set; }
    public string ModelProfileId { get; set; } = "local-dev";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
