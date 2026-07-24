using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Repositories;

public sealed class WordRepository(ApplicationDbContext db) : IWordRepository
{
    public async Task<IReadOnlyList<Word>> ListAsync(CancellationToken cancellationToken)
    {
        return await db.Words.AsNoTracking()
            .Include(word => word.Scenarios)
            .OrderBy(word => word.DifficultyLevel == DifficultyLevel.Basic ? 0 :
                word.DifficultyLevel == DifficultyLevel.Intermediate ? 1 : 2)
            .ThenBy(word => word.Lemma)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Word>> GetDailyWordsAsync(Guid userId, int count, CancellationToken cancellationToken)
    {
        var learnedWordIds = await db.UserWordRelationships
            .Where(relationship => relationship.UserId == userId)
            .Select(relationship => relationship.WordId)
            .ToListAsync(cancellationToken);

        return await db.Words.AsNoTracking()
            .Where(word => !learnedWordIds.Contains(word.Id))
            .OrderBy(word => word.DifficultyLevel == DifficultyLevel.Basic ? 0 :
                word.DifficultyLevel == DifficultyLevel.Intermediate ? 1 : 2)
            .ThenBy(word => word.Lemma)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public Task<Word?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return db.Words
            .Include(word => word.Scenarios)
            .FirstOrDefaultAsync(word => word.Id == id, cancellationToken);
    }

    public Task<Word?> GetByLemmaAsync(string lemma, CancellationToken cancellationToken)
    {
        return db.Words
            .Include(word => word.Scenarios)
            .FirstOrDefaultAsync(word => word.Lemma == lemma, cancellationToken);
    }

    public async Task AddAsync(Word word, CancellationToken cancellationToken)
    {
        await db.Words.AddAsync(word, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return db.SaveChangesAsync(cancellationToken);
    }
}
