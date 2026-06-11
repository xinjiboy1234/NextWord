using NextWord.Domain.Enums;

namespace NextWord.Domain.Entities;

public sealed class Sentence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? WordId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string TargetWord { get; set; } = string.Empty;
    public DifficultyLevel DifficultyLevel { get; set; } = DifficultyLevel.Basic;
    public CefrLevel CefrLevel { get; set; } = CefrLevel.A1;
    public string Scene { get; set; } = "life";
    public Guid? AnnotationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Word? Word { get; set; }
    public DifficultyAnnotation? Annotation { get; set; }
}
