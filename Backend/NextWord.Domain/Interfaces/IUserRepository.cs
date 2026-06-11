using NextWord.Domain.Entities;

namespace NextWord.Domain.Interfaces;

public interface IUserRepository
{
    Task<User> GetOrCreateDefaultUserAsync(CancellationToken cancellationToken);
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<UserWordRelationship> GetOrCreateRelationshipAsync(Guid userId, Guid wordId, CancellationToken cancellationToken);
    Task<UserProgress> GetOrCreateProgressAsync(Guid userId, CancellationToken cancellationToken);
    Task AddLearningLogAsync(WordLearningLog log, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
