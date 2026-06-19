namespace NextWord.Domain.Entities;

public sealed class ArticleComment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid ArticleId { get; set; }
    public int ParagraphIndex { get; set; }
    public string ParagraphText { get; set; } = string.Empty;
    public string CommentText { get; set; } = string.Empty;
    public string? AiReply { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public User? User { get; set; }
    public Article? Article { get; set; }
}
