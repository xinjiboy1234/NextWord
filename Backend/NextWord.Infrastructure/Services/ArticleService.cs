using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

public sealed class ArticleService(
    ApplicationDbContext db,
    ILearningPlanService learningPlans,
    IScoreProfileService scoreProfile) : IArticleService
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

    public async Task<RecommendedArticles> GetRecommendedAsync(Guid userId, CancellationToken cancellationToken)
    {
        // 优先执行当日 Plan 的阅读推荐（主攻场景选文）
        var active = await learningPlans.GetActiveAsync(userId, cancellationToken);
        if (active is not null && active.Content.ArticleIds.Count > 0)
        {
            var planned = await db.Articles.AsNoTracking()
                .Where(article => active.Content.ArticleIds.Contains(article.Id))
                .ToListAsync(cancellationToken);
            if (planned.Count > 0)
            {
                var ordered = active.Content.ArticleIds
                    .Select(id => planned.FirstOrDefault(article => article.Id == id))
                    .Where(article => article is not null)
                    .Cast<Article>()
                    .ToList();
                return new RecommendedArticles(ordered, true);
            }
        }

        // 回退：难度就近（用户永远有内容可学）
        var scores = await scoreProfile.GetScoresAsync(userId, cancellationToken);
        var userCefr = Enum.TryParse<CefrLevel>(scores.CefrDisplay, out var parsed) ? parsed : CefrLevel.A2;
        var articles = await db.Articles.AsNoTracking().ToListAsync(cancellationToken);
        var fallback = articles
            .OrderBy(article => Math.Abs((int)article.CefrLevel - (int)userCefr))
            .ThenBy(article => article.Title)
            .Take(3)
            .ToList();
        return new RecommendedArticles(fallback, false);
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
