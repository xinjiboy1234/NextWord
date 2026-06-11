using NextWord.Domain.Interfaces;
using NextWord.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace NextWord.Api.Endpoints;

public static class ProgressEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/progress").WithTags("Progress");

        group.MapGet("/", async (Guid? userId, IUserRepository users, ApplicationDbContext db, CancellationToken ct) =>
        {
            var user = userId.HasValue
                ? await users.GetByIdAsync(userId.Value, ct)
                : await users.GetOrCreateDefaultUserAsync(ct);
            if (user is null)
            {
                return Results.NotFound(new { message = "User not found." });
            }

            var progress = await users.GetOrCreateProgressAsync(user.Id, ct);
            var now = DateTimeOffset.UtcNow;
            var totalLearned = await db.UserWordRelationships.CountAsync(item => item.UserId == user.Id, ct);
            var reviewDueDates = await db.UserWordRelationships
                .Where(item => item.UserId == user.Id)
                .Select(item => item.NextReviewDue)
                .ToListAsync(ct);
            var dueReviews = reviewDueDates.Count(nextReviewDue => nextReviewDue <= now);
            var totalLogs = await db.WordLearningLogs.CountAsync(item => item.UserId == user.Id, ct);
            var correctLogs = await db.WordLearningLogs.CountAsync(item => item.UserId == user.Id && item.IsCorrect, ct);

            return Results.Ok(new ProgressDto(
                user.Id,
                user.DisplayName,
                progress.OverallLevel.ToString(),
                progress.VocabLevel.ToString(),
                progress.StreakDays,
                progress.LastStudyDate,
                totalLearned,
                dueReviews,
                totalLogs,
                totalLogs == 0 ? 0 : Math.Round((double)correctLogs / totalLogs * 100, 1)));
        });
    }
}

public sealed record ProgressDto(
    Guid UserId,
    string DisplayName,
    string OverallLevel,
    string VocabLevel,
    int StreakDays,
    DateOnly? LastStudyDate,
    int TotalLearned,
    int DueReviews,
    int TotalLogs,
    double AccuracyPercent);
