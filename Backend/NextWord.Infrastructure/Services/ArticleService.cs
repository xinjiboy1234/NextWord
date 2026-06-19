using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

public sealed class ArticleService(ApplicationDbContext db) : IArticleService
{
    public async Task<IReadOnlyList<Article>> ListAsync(DifficultyLevel? level, CefrLevel? cefr, CancellationToken cancellationToken)
    {
        var query = db.Articles.AsNoTracking().AsQueryable();
        if (level.HasValue)
        {
            query = query.Where(article => article.DifficultyLevel == level.Value);
        }

        if (cefr.HasValue)
        {
            query = query.Where(article => article.CefrLevel == cefr.Value);
        }

        return await query
            .OrderBy(article => article.DifficultyLevel)
            .ThenBy(article => article.Title)
            .ToListAsync(cancellationToken);
    }

    public Task<Article?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return db.Articles
            .AsNoTracking()
            .Include(article => article.VocabMappings)
            .FirstOrDefaultAsync(article => article.Id == id, cancellationToken);
    }

    public async Task<ReadingLog> StartReadingAsync(Guid userId, Guid articleId, CancellationToken cancellationToken)
    {
        var articleExists = await db.Articles.AnyAsync(article => article.Id == articleId, cancellationToken);
        if (!articleExists)
        {
            throw new InvalidOperationException("Article not found.");
        }

        var log = new ReadingLog
        {
            UserId = userId,
            ArticleId = articleId,
            StartTime = DateTimeOffset.UtcNow
        };
        db.ReadingLogs.Add(log);
        await db.SaveChangesAsync(cancellationToken);
        return log;
    }

    public async Task<ReadingLog?> FinishReadingAsync(Guid logId, int lookupCount, int commentsCount, CancellationToken cancellationToken)
    {
        var log = await db.ReadingLogs.FirstOrDefaultAsync(item => item.Id == logId, cancellationToken);
        if (log is null)
        {
            return null;
        }

        log.EndTime = DateTimeOffset.UtcNow;
        log.LookupCount = Math.Max(0, lookupCount);
        log.CommentsCount = Math.Max(0, commentsCount);
        log.DurationSeconds = Math.Max(0, (int)(log.EndTime.Value - log.StartTime).TotalSeconds);
        await db.SaveChangesAsync(cancellationToken);
        return log;
    }

    public async Task IncrementLookupAsync(Guid logId, CancellationToken cancellationToken)
    {
        var log = await db.ReadingLogs.FirstOrDefaultAsync(item => item.Id == logId, cancellationToken);
        if (log is null)
        {
            return;
        }

        log.LookupCount += 1;
        await db.SaveChangesAsync(cancellationToken);
    }
}
