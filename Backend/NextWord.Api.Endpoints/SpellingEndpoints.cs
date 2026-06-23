using NextWord.Domain.Entities;
using NextWord.Domain.Interfaces;

namespace NextWord.Api.Endpoints;

public static class SpellingEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/spelling").WithTags("Spelling");

        group.MapGet("/queue", async (
            HttpContext http,
            int? count,
            IUserRepository users,
            IReviewQueueService reviews,
            IWordRepository words,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(http, null, users, ct);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            var due = await reviews.GetDueReviewsAsync(user.Id, Math.Clamp(count ?? 8, 1, 20), ct);
            if (due.Count > 0)
            {
                return Results.Ok(due.Where(item => item.Word is not null).Select(item => WordDto.FromEntity(item.Word!)));
            }

            var fallback = await words.GetDailyWordsAsync(user.Id, Math.Clamp(count ?? 8, 1, 20), ct);
            return Results.Ok(fallback.Select(WordDto.FromEntity));
        });

        group.MapPost("/submit", async (
            HttpContext http,
            SubmitSpellingRequest request,
            IUserRepository users,
            ISpellingService spelling,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.UserSpelling))
            {
                return Results.BadRequest(new { message = "Spelling answer is required." });
            }

            var user = await UserResolver.ResolveAsync(http, request.UserId, users, ct);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            try
            {
                var log = await spelling.SubmitAsync(user.Id, request.WordId, request.UserSpelling, request.Attempts ?? 1, ct);
                return Results.Ok(SpellingLogDto.FromEntity(log));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(new { message = "Word not found." });
            }
        });
    }
}

public sealed record SubmitSpellingRequest(Guid? UserId, Guid WordId, string UserSpelling, int? Attempts);

public sealed record SpellingLogDto(
    Guid Id,
    Guid WordId,
    string UserSpelling,
    string CorrectSpelling,
    bool IsCorrect,
    IReadOnlyList<int> ErrorPositions,
    DateTimeOffset Timestamp,
    int Attempts)
{
    public static SpellingLogDto FromEntity(SpellingLog log)
    {
        return new SpellingLogDto(
            log.Id,
            log.WordId,
            log.UserSpelling,
            log.CorrectSpelling,
            log.IsCorrect,
            log.ErrorPositions,
            log.Timestamp,
            log.Attempts);
    }
}
