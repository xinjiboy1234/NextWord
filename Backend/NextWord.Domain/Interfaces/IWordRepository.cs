using NextWord.Domain.Entities;

namespace NextWord.Domain.Interfaces;

public interface IWordRepository
{
    Task<IReadOnlyList<Word>> ListAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Word>> GetDailyWordsAsync(Guid userId, int count, CancellationToken cancellationToken);
    Task<Word?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Word?> GetByLemmaAsync(string lemma, CancellationToken cancellationToken);
    Task AddAsync(Word word, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
