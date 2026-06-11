using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Interfaces;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Repositories;

public sealed class ReviewQueueService(ApplicationDbContext db) : IReviewQueueService
{
    public async Task<IReadOnlyList<UserWordRelationship>> GetDueReviewsAsync(Guid userId, int count, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var relationships = await db.UserWordRelationships
            .AsNoTracking()
            .Include(relationship => relationship.Word)
            .Where(relationship => relationship.UserId == userId)
            .ToListAsync(cancellationToken);

        return relationships
            .Where(relationship => relationship.NextReviewDue <= now)
            .OrderBy(relationship => relationship.NextReviewDue)
            .Take(count)
            .ToList();
    }
}
