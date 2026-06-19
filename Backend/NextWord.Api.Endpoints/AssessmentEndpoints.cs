using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Infrastructure.Services;
using NextWord.Domain.Models;

namespace NextWord.Api.Endpoints;

public static class AssessmentEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/assessment").WithTags("Assessment");

        group.MapPost("/initial/start", async (AssessmentUserRequest request, IUserRepository users, IAssessmentService assessment, CancellationToken ct) =>
        {
            var user = await ResolveUserAsync(request.UserId, users, ct);
            if (user is null) return Results.NotFound();
            var item = await assessment.StartInitialAsync(user.Id, ct);
            return Results.Ok(new { assessmentId = item.Id, status = item.Status.ToString() });
        });

        group.MapGet("/{assessmentId:guid}/step/{step:int}", async (Guid assessmentId, int step, IAssessmentService assessment, CancellationToken ct) =>
        {
            if (!Enum.IsDefined(typeof(AssessmentStepType), step))
            {
                return Results.BadRequest(new { message = "Invalid step." });
            }

            var questions = await assessment.GetStepQuestionsAsync(assessmentId, (AssessmentStepType)step, ct);
            return Results.Ok(questions);
        });

        group.MapPost("/{assessmentId:guid}/step/{step:int}", async (
            Guid assessmentId,
            int step,
            StepSubmitRequest request,
            IAssessmentService assessment,
            CancellationToken ct) =>
        {
            if (!Enum.IsDefined(typeof(AssessmentStepType), step))
            {
                return Results.BadRequest(new { message = "Invalid step." });
            }

            var result = await assessment.SubmitStepAsync(assessmentId, (AssessmentStepType)step, request.AnswersJson, ct);
            return Results.Ok(result);
        });

        group.MapPost("/{assessmentId:guid}/complete", async (Guid assessmentId, IAssessmentService assessment, CancellationToken ct) =>
        {
            var final = await assessment.CompleteInitialAsync(assessmentId, ct);
            return final is null ? Results.BadRequest(new { message = "Complete all steps first." }) : Results.Ok(final);
        });

        group.MapGet("/{assessmentId:guid}", async (Guid assessmentId, IAssessmentService assessment, CancellationToken ct) =>
        {
            var item = await assessment.GetAsync(assessmentId, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });
    }

    private static async Task<Domain.Entities.User?> ResolveUserAsync(Guid? userId, IUserRepository users, CancellationToken ct)
        => userId.HasValue ? await users.GetByIdAsync(userId.Value, ct) : await users.GetOrCreateDefaultUserAsync(ct);
}

public static class ChallengeEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/challenge").WithTags("Challenge");

        group.MapPost("/start", async (ChallengeStartRequest request, IUserRepository users, IChallengeService challenge, CancellationToken ct) =>
        {
            var user = userId(request.UserId, users, ct);
            var resolved = await user;
            if (resolved is null) return Results.NotFound();
            var pack = await challenge.StartChallengeAsync(resolved.Id, request.ConfirmationChallenge, ct);
            return Results.Ok(pack);
        });

        group.MapPost("/submit", async (ChallengeSubmitRequest request, IUserRepository users, IChallengeService challenge, CancellationToken ct) =>
        {
            var resolved = await (request.UserId.HasValue
                ? users.GetByIdAsync(request.UserId.Value, ct)
                : users.GetOrCreateDefaultUserAsync(ct));
            if (resolved is null) return Results.NotFound();
            var record = await challenge.SubmitChallengeAsync(
                resolved.Id,
                request.ChallengeType,
                request.VocabularyScore,
                request.SentenceScore,
                request.ReadingScore,
                request.ConfirmationChallenge,
                ct);
            return Results.Ok(record);
        });

        group.MapGet("/recent", async (Guid? userId, IUserRepository users, IChallengeService challenge, CancellationToken ct) =>
        {
            var resolved = await (userId.HasValue
                ? users.GetByIdAsync(userId.Value, ct)
                : users.GetOrCreateDefaultUserAsync(ct));
            if (resolved is null) return Results.NotFound();
            var records = await challenge.GetRecentAsync(resolved.Id, 10, ct);
            return Results.Ok(records);
        });
    }

    private static Task<Domain.Entities.User?> userId(Guid? id, IUserRepository users, CancellationToken ct)
        => id.HasValue ? users.GetByIdAsync(id.Value, ct) : users.GetOrCreateDefaultUserAsync(ct);
}

public static class LevelEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/level").WithTags("Level");

        group.MapGet("/dashboard", async (Guid? userId, IUserRepository users, LevelDashboardService dashboard, CancellationToken ct) =>
        {
            var resolved = await (userId.HasValue
                ? users.GetByIdAsync(userId.Value, ct)
                : users.GetOrCreateDefaultUserAsync(ct));
            if (resolved is null) return Results.NotFound();
            var data = await dashboard.GetDashboardAsync(resolved.Id, ct);
            return Results.Ok(data);
        });

        group.MapGet("/history", async (Guid? userId, IUserRepository users, LevelDashboardService dashboard, CancellationToken ct) =>
        {
            var resolved = await (userId.HasValue
                ? users.GetByIdAsync(userId.Value, ct)
                : users.GetOrCreateDefaultUserAsync(ct));
            if (resolved is null) return Results.NotFound();
            var data = await dashboard.GetHistoryAsync(resolved.Id, ct);
            return Results.Ok(data);
        });
    }
}

public sealed record AssessmentUserRequest(Guid? UserId);
public sealed record StepSubmitRequest(string AnswersJson);
public sealed record ChallengeStartRequest(Guid? UserId, bool ConfirmationChallenge = false);
public sealed record ChallengeSubmitRequest(
    Guid? UserId,
    ChallengeType ChallengeType,
    double VocabularyScore,
    double SentenceScore,
    double ReadingScore,
    bool ConfirmationChallenge = false);
