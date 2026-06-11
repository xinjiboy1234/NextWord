using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;

namespace NextWord.Api.Endpoints;

public static class FreeExpressionEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/free-expression").WithTags("Free Expression");

        group.MapPost("/rate", async (
            RateFreeExpressionRequest request,
            IUserRepository users,
            IFreeExpressionService expressions,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.UserText))
            {
                return Results.BadRequest(new { message = "Text is required." });
            }

            var user = request.UserId.HasValue
                ? await users.GetByIdAsync(request.UserId.Value, ct)
                : await users.GetOrCreateDefaultUserAsync(ct);
            if (user is null)
            {
                return Results.NotFound(new { message = "User not found." });
            }

            var log = await expressions.RateAsync(user.Id, request.UserText, request.UserLevel ?? "A2", ct);
            return Results.Ok(FreeExpressionLogDto.FromEntity(log));
        });
    }
}

public sealed record RateFreeExpressionRequest(Guid? UserId, string UserText, string? UserLevel);

public sealed record FreeExpressionLogDto(
    Guid Id,
    string UserText,
    int AiScore,
    string OverallGrade,
    string AiRevision,
    IReadOnlyList<string> ErrorSentences,
    IReadOnlyList<string> Suggestions,
    DifficultyLevel DifficultyLevel,
    DateTimeOffset Timestamp)
{
    public static FreeExpressionLogDto FromEntity(FreeExpressionLog log)
    {
        return new FreeExpressionLogDto(
            log.Id,
            log.UserText,
            log.AiScore,
            log.OverallGrade,
            log.AiRevision,
            log.ErrorSentences,
            log.Suggestions,
            log.DifficultyLevel,
            log.Timestamp);
    }
}
