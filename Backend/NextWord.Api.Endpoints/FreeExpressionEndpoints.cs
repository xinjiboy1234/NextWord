using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Infrastructure.Services;

namespace NextWord.Api.Endpoints;

public static class FreeExpressionEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/free-expression").WithTags("Free Expression");

        group.MapPost("/rate", async (
            HttpContext http,
            RateFreeExpressionRequest request,
            IUserRepository users,
            IFreeExpressionService expressions,
            PracticeScoreWritebackService scoreWriteback,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.UserText))
            {
                return Results.BadRequest(new { message = "Text is required." });
            }

            var user = await UserResolver.ResolveAsync(http, request.UserId, users, ct);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            // T-027：评分带由服务端从 UserProgress 解析，客户端 userLevel 仅作无进度回退
            var log = await expressions.RateAsync(user.Id, request.UserText, request.UserLevel, ct);
            // T-022：用户主动练习的自由表达评分小步回写 Writing 维
            var writing = await scoreWriteback.ApplyFreeExpressionAsync(user.Id, log, ct);
            return Results.Ok(FreeExpressionLogDto.FromEntity(log, writing));
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
    DateTimeOffset Timestamp,
    int? WritingScoreBefore,
    int? WritingScoreAfter)
{
    public static FreeExpressionLogDto FromEntity(FreeExpressionLog log, WritingScoreChange? writing = null)
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
            log.Timestamp,
            writing?.Before,
            writing?.After);
    }
}
