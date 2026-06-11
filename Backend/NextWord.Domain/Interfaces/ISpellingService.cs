using NextWord.Domain.Entities;

namespace NextWord.Domain.Interfaces;

public interface ISpellingService
{
    Task<SpellingLog> SubmitAsync(Guid userId, Guid wordId, string userSpelling, int attempts, CancellationToken cancellationToken);
}
