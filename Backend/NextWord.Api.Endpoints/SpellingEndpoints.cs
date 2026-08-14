using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
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
            string? mode,
            IUserRepository users,
            ISpellingService spelling,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(http, null, users, ct);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            // T-052：mode=review|new|mixed（默认 mixed，非法值回退 mixed）；T-051：默认题量 12（上限 20 不变）
            var queueMode = mode?.Trim().ToLowerInvariant() switch
            {
                "review" => SpellingQueueMode.Review,
                "new" => SpellingQueueMode.New,
                _ => SpellingQueueMode.Mixed,
            };
            var queue = await spelling.GetQueueAsync(user.Id, Math.Clamp(count ?? 12, 1, 20), queueMode, ct);
            return Results.Ok(queue.Select(item => SpellingQueueItemDto.FromEntity(item.Word, item.IsReview)));
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

/// <summary>
/// T-052：拼写队列项 = 词信息 + IsReview 来源标记（true 到期复习 / false 带内新词，前端徽标用）。
/// 字段与 WordDto 单词信息部分对齐，/review 页等旧用法按单词字段读取不受影响。
/// </summary>
public sealed record SpellingQueueItemDto(
    Guid Id,
    string Lemma,
    string PartOfSpeech,
    string Phonetics,
    IReadOnlyList<string> Meanings,
    IReadOnlyList<string> ExampleSentences,
    DifficultyLevel DifficultyLevel,
    CefrLevel CefrLevel,
    bool IsCore,
    bool IsReview)
{
    public static SpellingQueueItemDto FromEntity(Word word, bool isReview)
    {
        return new SpellingQueueItemDto(
            word.Id,
            word.Lemma,
            word.PartOfSpeech,
            word.Phonetics,
            word.Meanings,
            word.ExampleSentences,
            word.DifficultyLevel,
            word.CefrLevel,
            word.IsCore,
            isReview);
    }
}

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
