using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Infrastructure.Services;

namespace NextWord.Api.Endpoints;

public static class SentenceEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sentences").WithTags("Sentences");

        group.MapGet("/prompts", async (int? count, HttpContext http, ISentenceService sentences, CancellationToken ct) =>
        {
            // 登录用户走个性化出题（T-006：Plan 造句目标 / 带内约束回退）；匿名保持既有出题
            var userId = UserResolver.GetAuthenticatedUserId(http);
            if (userId.HasValue)
            {
                var batch = await sentences.GetPersonalizedPromptsAsync(userId.Value, count ?? 10, ct);
                return Results.Ok(batch.Prompts.Select(prompt => SentencePromptDto.FromEntity(prompt, batch.FromPlan)));
            }

            var prompts = await sentences.GetPromptsAsync(count ?? 10, ct);
            return Results.Ok(prompts.Select(prompt => SentencePromptDto.FromEntity(prompt, false)));
        });

        group.MapPost("/rate", async (
            HttpContext http,
            RateSentenceRequest request,
            IUserRepository users,
            ISentenceService sentences,
            PracticeScoreWritebackService scoreWriteback,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.UserSentence) || string.IsNullOrWhiteSpace(request.TargetWord))
            {
                return Results.BadRequest(new { message = "Target word and sentence are required." });
            }

            var user = await UserResolver.ResolveAsync(http, request.UserId, users, ct);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            var log = await sentences.RateAsync(
                user.Id,
                request.WordId,
                request.TargetWord,
                request.UserSentence,
                request.Scene ?? "life",
                request.UserLevel ?? "A2",
                ct);

            // T-022：用户主动练习的造句评分小步回写 Writing 维（测评/挑战路径不经过此端点）
            var writing = await scoreWriteback.ApplySentenceAsync(user.Id, log, ct);
            return Results.Ok(SentenceLogDto.FromEntity(log, writing));
        });
    }
}

public sealed record RateSentenceRequest(
    Guid? UserId,
    Guid? WordId,
    string TargetWord,
    string UserSentence,
    string? Scene,
    string? UserLevel);

public sealed record SentencePromptDto(
    Guid Id,
    Guid? WordId,
    string Content,
    string TargetWord,
    DifficultyLevel DifficultyLevel,
    CefrLevel CefrLevel,
    string Scene,
    bool FromPlan)
{
    public static SentencePromptDto FromEntity(Sentence sentence, bool fromPlan = false)
    {
        return new SentencePromptDto(
            sentence.Id,
            sentence.WordId,
            sentence.Content,
            sentence.TargetWord,
            sentence.DifficultyLevel,
            sentence.CefrLevel,
            sentence.Scene,
            fromPlan);
    }
}

public sealed record SentenceLogDto(
    Guid Id,
    Guid? WordId,
    string TargetWord,
    string Scene,
    string UserSentence,
    string AiRevision,
    int GrammarScore,
    int NaturalScore,
    int VocabularyScore,
    int RelevanceScore,
    string OverallGrade,
    IReadOnlyList<string> ErrorTags,
    DifficultyLevel DifficultyLevel,
    string Suggestion,
    DateTimeOffset Timestamp,
    int? WritingScoreBefore,
    int? WritingScoreAfter)
{
    public static SentenceLogDto FromEntity(SentenceLog log, WritingScoreChange? writing = null)
    {
        return new SentenceLogDto(
            log.Id,
            log.WordId,
            log.TargetWord,
            log.Scene,
            log.UserSentence,
            log.AiRevision,
            log.GrammarScore,
            log.NaturalScore,
            log.VocabularyScore,
            log.RelevanceScore,
            log.OverallGrade,
            log.ErrorTags,
            log.DifficultyLevel,
            log.Suggestion,
            log.Timestamp,
            writing?.Before,
            writing?.After);
    }
}
