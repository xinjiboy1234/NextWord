using Microsoft.EntityFrameworkCore;
using NextWord.Infrastructure.Data;

namespace NextWord.Api.Endpoints;

public static class LogEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/logs").WithTags("Logs");

        group.MapGet("/summary", async (HttpContext http, ApplicationDbContext db, CancellationToken ct) =>
        {
            var resolvedUserId = UserResolver.GetAuthenticatedUserId(http);
            if (!resolvedUserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var sentenceCount = await db.SentenceLogs.CountAsync(log => log.UserId == resolvedUserId, ct);
            var freeExpressionCount = await db.FreeExpressionLogs.CountAsync(log => log.UserId == resolvedUserId, ct);
            var spellingCount = await db.SpellingLogs.CountAsync(log => log.UserId == resolvedUserId, ct);
            var spellingCorrect = await db.SpellingLogs.CountAsync(log => log.UserId == resolvedUserId && log.IsCorrect, ct);
            var now = DateTimeOffset.UtcNow;
            var reviewDates = await db.UserWordRelationships
                .AsNoTracking()
                .Where(item => item.UserId == resolvedUserId)
                .Select(item => item.NextReviewDue)
                .ToListAsync(ct);
            var dueReviews = reviewDates.Count(item => item <= now);

            return Results.Ok(new LogSummaryDto(
                sentenceCount,
                freeExpressionCount,
                spellingCount,
                spellingCount == 0 ? 0 : (int)Math.Round(spellingCorrect * 100.0 / spellingCount),
                dueReviews));
        });

        group.MapGet("/recent", async (HttpContext http, int? count, ApplicationDbContext db, CancellationToken ct) =>
        {
            var resolvedUserId = UserResolver.GetAuthenticatedUserId(http);
            if (!resolvedUserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var take = Math.Clamp(count ?? 12, 1, 30);
            var sentenceLogs = await db.SentenceLogs
                .AsNoTracking()
                .Where(log => log.UserId == resolvedUserId)
                .OrderByDescending(log => log.Timestamp)
                .Take(take)
                .Select(log => new RecentLogDto("sentence", log.TargetWord, log.OverallGrade, log.Timestamp))
                .ToListAsync(ct);
            var spellingLogs = await db.SpellingLogs
                .AsNoTracking()
                .Where(log => log.UserId == resolvedUserId)
                .OrderByDescending(log => log.Timestamp)
                .Take(take)
                .Select(log => new RecentLogDto("spelling", log.CorrectSpelling, log.IsCorrect ? "correct" : "missed", log.Timestamp))
                .ToListAsync(ct);

            return Results.Ok(sentenceLogs.Concat(spellingLogs).OrderByDescending(log => log.Timestamp).Take(take));
        });
    }
}

public sealed record LogSummaryDto(
    int SentenceCount,
    int FreeExpressionCount,
    int SpellingCount,
    int SpellingAccuracyPercent,
    int DueReviews);

public sealed record RecentLogDto(string Type, string Label, string Result, DateTimeOffset Timestamp);
