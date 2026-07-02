using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Infrastructure.Data;

namespace NextWord.Api.Endpoints;

public static class ScoreKernelEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var reading = app.MapGroup("/api/reading").WithTags("Reading");
        reading.MapPost("/lookup", async (HttpContext http, ReadingLookupBody body, IUserRepository users, IReadingLookupService lookup, CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(http, body.UserId, users, ct);
            if (user is null) return Results.Unauthorized();
            var result = await lookup.LookupAsync(user.Id, new ReadingLookupRequest(body.Word, body.Sentence, body.ArticleId), ct);
            return Results.Ok(result);
        });

        var evaluation = app.MapGroup("/api/evaluation").WithTags("Evaluation");
        evaluation.MapGet("/latest", async (HttpContext http, Guid? userId, IUserRepository users, ApplicationDbContext db, CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(http, userId, users, ct);
            if (user is null) return Results.Unauthorized();
            var report = await db.EvaluationReports.AsNoTracking()
                .Where(item => item.UserId == user.Id)
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefaultAsync(ct);
            return report is null ? Results.NotFound() : Results.Ok(report);
        });

        evaluation.MapGet("/{id:long}", async (long id, HttpContext http, Guid? userId, IUserRepository users, ApplicationDbContext db, CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(http, userId, users, ct);
            if (user is null) return Results.Unauthorized();
            var report = await db.EvaluationReports.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id && item.UserId == user.Id, ct);
            return report is null ? Results.NotFound() : Results.Ok(report);
        });

        var feedback = app.MapGroup("/api/feedback").WithTags("Feedback");
        feedback.MapPost("/", async (HttpContext http, FeedbackBody body, IUserRepository users, IUserFeedbackService service, CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(http, body.UserId, users, ct);
            if (user is null) return Results.Unauthorized();
            await service.SubmitAsync(user.Id, body.FeedbackType, body.TargetWord, body.ContextJson, ct);
            return Results.Ok(new { status = "accepted" });
        });

        var tools = app.MapGroup("/api/tools").WithTags("Tools");
        tools.MapGet("/", (ILearningToolRegistry registry) => Results.Ok(registry.ToolNames));
        tools.MapPost("/{name}", async (string name, HttpContext http, JsonElement body, Guid? userId, IUserRepository users, ILearningToolRegistry registry, CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(http, userId, users, ct);
            if (user is null) return Results.Unauthorized();
            var result = await registry.InvokeAsync(name, body, user.Id, ct);
            return Results.Ok(result);
        });
    }
}

public sealed record ReadingLookupBody(Guid? UserId, string Word, string Sentence, Guid? ArticleId);
public sealed record FeedbackBody(Guid? UserId, string FeedbackType, string TargetWord, string? ContextJson);
