using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;

namespace NextWord.Api.Endpoints;

public static class SentenceEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sentences").WithTags("Sentences");

        group.MapGet("/prompts", async (int? count, ISentenceService sentences, CancellationToken ct) =>
        {
            var prompts = await sentences.GetPromptsAsync(count ?? 10, ct);
            return Results.Ok(prompts.Select(SentencePromptDto.FromEntity));
        });

        group.MapPost("/rate", async (
            RateSentenceRequest request,
            IUserRepository users,
            ISentenceService sentences,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.UserSentence) || string.IsNullOrWhiteSpace(request.TargetWord))
            {
                return Results.BadRequest(new { message = "Target word and sentence are required." });
            }

            var user = request.UserId.HasValue
                ? await users.GetByIdAsync(request.UserId.Value, ct)
                : await users.GetOrCreateDefaultUserAsync(ct);
            if (user is null)
            {
                return Results.NotFound(new { message = "User not found." });
            }

            var log = await sentences.RateAsync(
                user.Id,
                request.WordId,
                request.TargetWord,
                request.UserSentence,
                request.Scene ?? "life",
                request.UserLevel ?? "A2",
                ct);

            return Results.Ok(SentenceLogDto.FromEntity(log));
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
    string Scene)
{
    public static SentencePromptDto FromEntity(Sentence sentence)
    {
        return new SentencePromptDto(
            sentence.Id,
            sentence.WordId,
            sentence.Content,
            sentence.TargetWord,
            sentence.DifficultyLevel,
            sentence.CefrLevel,
            sentence.Scene);
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
    DateTimeOffset Timestamp)
{
    public static SentenceLogDto FromEntity(SentenceLog log)
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
            log.Timestamp);
    }
}
