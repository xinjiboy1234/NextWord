using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

public sealed class CommentService(ApplicationDbContext db, ILLMProvider llm) : ICommentService
{
    public async Task<IReadOnlyList<ArticleComment>> ListAsync(Guid articleId, CancellationToken cancellationToken)
    {
        var comments = await db.ArticleComments
            .AsNoTracking()
            .Where(comment => comment.ArticleId == articleId)
            .ToListAsync(cancellationToken);

        return comments
            .OrderBy(comment => comment.ParagraphIndex)
            .ThenBy(comment => comment.Timestamp)
            .ToList();
    }

    public async Task<ArticleComment> AddAsync(
        Guid userId,
        Guid articleId,
        int paragraphIndex,
        string paragraphText,
        string commentText,
        bool requestAiReply,
        CancellationToken cancellationToken)
    {
        var article = await db.Articles.AsNoTracking().FirstOrDefaultAsync(item => item.Id == articleId, cancellationToken)
            ?? throw new InvalidOperationException("Article not found.");

        var comment = new ArticleComment
        {
            UserId = userId,
            ArticleId = articleId,
            ParagraphIndex = Math.Max(0, paragraphIndex),
            ParagraphText = paragraphText.Trim(),
            CommentText = commentText.Trim()
        };

        if (requestAiReply)
        {
            var reply = await llm.ReplyToCommentAsync(new CommentReplyRequest(
                comment.ParagraphText,
                comment.CommentText,
                article.Title,
                new LlmRequestOptions("reading-agent", "comment_reply")), cancellationToken);
            comment.AiReply = reply.Reply;
        }

        db.ArticleComments.Add(comment);
        await db.SaveChangesAsync(cancellationToken);
        return comment;
    }
}
