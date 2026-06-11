using NextWord.Domain.Entities;

namespace NextWord.Domain.Interfaces;

public interface IReviewQueueService
{
    Task<IReadOnlyList<UserWordRelationship>> GetDueReviewsAsync(Guid userId, int count, CancellationToken cancellationToken);
}
