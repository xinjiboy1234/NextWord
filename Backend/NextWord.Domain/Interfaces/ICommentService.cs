using NextWord.Domain.Entities;

namespace NextWord.Domain.Interfaces;

public interface ICommentService
{
    Task<IReadOnlyList<ArticleComment>> ListAsync(Guid articleId, CancellationToken cancellationToken);
    Task<ArticleComment> AddAsync(Guid userId, Guid articleId, int paragraphIndex, string paragraphText, string commentText, bool requestAiReply, CancellationToken cancellationToken);
}
