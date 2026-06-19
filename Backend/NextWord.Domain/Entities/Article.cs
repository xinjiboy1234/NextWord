using NextWord.Domain.Enums;

namespace NextWord.Domain.Entities;

public sealed class Article
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DifficultyLevel DifficultyLevel { get; set; } = DifficultyLevel.Basic;
    public CefrLevel CefrLevel { get; set; } = CefrLevel.A1;
    public int WordCount { get; set; }
    public ArticleSource Source { get; set; } = ArticleSource.Builtin;
    public Guid? AnnotationId { get; set; }
    public string? TopicTag { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DifficultyAnnotation? Annotation { get; set; }
    public List<ReadingLog> ReadingLogs { get; set; } = [];
    public List<ArticleComment> Comments { get; set; } = [];
    public List<ArticleVocabMapping> VocabMappings { get; set; } = [];
}
