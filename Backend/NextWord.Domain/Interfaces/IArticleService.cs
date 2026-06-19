using NextWord.Domain.Entities;
using NextWord.Domain.Enums;

namespace NextWord.Domain.Interfaces;

public interface IArticleService
{
    Task<IReadOnlyList<Article>> ListAsync(DifficultyLevel? level, CefrLevel? cefr, CancellationToken cancellationToken);
    Task<Article?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ReadingLog> StartReadingAsync(Guid userId, Guid articleId, CancellationToken cancellationToken);
    Task<ReadingLog?> FinishReadingAsync(Guid logId, int lookupCount, int commentsCount, CancellationToken cancellationToken);
    Task IncrementLookupAsync(Guid logId, CancellationToken cancellationToken);
}
