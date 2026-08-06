using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Infrastructure.Services;

namespace NextWord.Api.Endpoints;

public static class AssessmentEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/assessment").WithTags("Assessment");

        group.MapPost("/initial/start", async (HttpContext http, AssessmentUserRequest request, IUserRepository users, IAssessmentService assessment, CancellationToken ct) =>
        {
            var user = await ResolveUserAsync(http, request.UserId, users, ct);
            if (user is null) return Results.Unauthorized();
            var item = await assessment.StartInitialAsync(user.Id, ct);
            return Results.Ok(new { assessmentId = item.Id, status = item.Status.ToString() });
        });

        group.MapPost("/initial/skip", async (HttpContext http, AssessmentUserRequest request, IUserRepository users, IAssessmentService assessment, CancellationToken ct) =>
        {
            var user = await ResolveUserAsync(http, request.UserId, users, ct);
            if (user is null) return Results.Unauthorized();
            await assessment.SkipInitialAsync(user.Id, ct);
            return Results.Ok(new { skipped = true, defaultLevel = "A2" });
        });

        group.MapGet("/{assessmentId:guid}/next-block", async (Guid assessmentId, IAssessmentService assessment, CancellationToken ct) =>
        {
            var response = await assessment.GetNextBlockAsync(assessmentId, ct);
            return Results.Ok(response);
        });

        group.MapPost("/{assessmentId:guid}/blocks/{blockIndex:int}/submit", async (
            Guid assessmentId,
            int blockIndex,
            BlockSubmitRequest request,
            IAssessmentService assessment,
            CancellationToken ct) =>
        {
            var result = await assessment.SubmitBlockAsync(assessmentId, blockIndex, request.Answers, ct);
            return Results.Ok(result);
        });

        group.MapGet("/{assessmentId:guid}", async (Guid assessmentId, IAssessmentService assessment, CancellationToken ct) =>
        {
            var item = await assessment.GetAsync(assessmentId, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });
    }

    private static Task<Domain.Entities.User?> ResolveUserAsync(HttpContext http, Guid? userId, IUserRepository users, CancellationToken ct)
        => UserResolver.ResolveAsync(http, userId, users, ct);
}

public static class ChallengeEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/challenge").WithTags("Challenge");

        group.MapPost("/start", async (HttpContext http, ChallengeStartRequest request, IUserRepository users, IChallengeService challenge, CancellationToken ct) =>
        {
            var resolved = await UserResolver.ResolveAsync(http, request.UserId, users, ct);
            if (resolved is null) return Results.Unauthorized();
            var pack = await challenge.StartChallengeAsync(resolved.Id, request.ConfirmationChallenge, ct);
            return Results.Ok(pack);
        });

        group.MapPost("/submit", async (HttpContext http, ChallengeSubmitBody request, IUserRepository users, IChallengeService challenge, CancellationToken ct) =>
        {
            var resolved = await UserResolver.ResolveAsync(http, request.UserId, users, ct);
            if (resolved is null) return Results.Unauthorized();
            var record = await challenge.SubmitChallengeAsync(
                resolved.Id,
                new ChallengeSubmitRequest(
                    request.ChallengeSessionId,
                    request.ChallengeType,
                    request.VocabAnswers,
                    request.SentenceAnswer,
                    request.TargetWord,
                    request.Scene,
                    request.SentenceWordId,
                    request.ReadingSelectedIndex,
                    request.LookupCount)
                { ReadingSelectedIndexes = request.ReadingSelectedIndexes },
                ct);
            return Results.Ok(record);
        });

        group.MapGet("/recent", async (HttpContext http, Guid? userId, IUserRepository users, IChallengeService challenge, CancellationToken ct) =>
        {
            var resolved = await UserResolver.ResolveAsync(http, userId, users, ct);
            if (resolved is null) return Results.Unauthorized();
            var records = await challenge.GetRecentAsync(resolved.Id, 10, ct);
            return Results.Ok(records);
        });
    }
}

public static class LevelEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/level").WithTags("Level");

        group.MapGet("/dashboard", async (HttpContext http, Guid? userId, IUserRepository users, LevelDashboardService dashboard, CancellationToken ct) =>
        {
            var resolved = await UserResolver.ResolveAsync(http, userId, users, ct);
            if (resolved is null) return Results.Unauthorized();
            var data = await dashboard.GetDashboardAsync(resolved.Id, ct);
            return Results.Ok(data);
        });

        group.MapGet("/history", async (HttpContext http, Guid? userId, IUserRepository users, LevelDashboardService dashboard, CancellationToken ct) =>
        {
            var resolved = await UserResolver.ResolveAsync(http, userId, users, ct);
            if (resolved is null) return Results.Unauthorized();
            var data = await dashboard.GetHistoryAsync(resolved.Id, ct);
            return Results.Ok(data);
        });
    }
}

public sealed record AssessmentUserRequest(Guid? UserId);
public sealed record BlockSubmitRequest(IReadOnlyList<AssessmentAnswerItem> Answers);
public sealed record ChallengeStartRequest(Guid? UserId, bool ConfirmationChallenge = false);
public sealed record ChallengeSubmitBody(
    Guid? UserId,
    Guid ChallengeSessionId,
    ChallengeType ChallengeType,
    IReadOnlyList<int> VocabAnswers,
    string SentenceAnswer,
    string TargetWord,
    string Scene,
    Guid? SentenceWordId,
    int? ReadingSelectedIndex,
    int LookupCount)
{
    /// <summary>T-035：阅读 3 题作答；缺省时回退单题 ReadingSelectedIndex（旧客户端）。</summary>
    public IReadOnlyList<int>? ReadingSelectedIndexes { get; init; }
}
