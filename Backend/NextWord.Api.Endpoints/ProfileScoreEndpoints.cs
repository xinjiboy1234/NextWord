using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Interfaces;
using NextWord.Infrastructure.Data;

namespace NextWord.Api.Endpoints;

public static class ProfileScoreEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/profile").WithTags("Profile");

        group.MapGet("/scores", async (HttpContext http, Guid? userId, IUserRepository users, IScoreProfileService scores, CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(http, userId, users, ct);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            var profile = await scores.GetScoresAsync(user.Id, ct);
            return Results.Ok(ProfileScoresDto.From(profile));
        });

        group.MapGet("/scores/history", async (HttpContext http, Guid? userId, int? days, IUserRepository users, ApplicationDbContext db, CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(http, userId, users, ct);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            var window = Math.Clamp(days ?? 30, 1, 365);
            var since = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime.AddDays(-window));
            var snapshots = await db.ProfileScoreSnapshots.AsNoTracking()
                .Where(item => item.UserId == user.Id && item.Date >= since)
                .OrderByDescending(item => item.Date)
                .Select(item => new ProfileScoreSnapshotDto(item.Date, item.ScoresJson))
                .ToListAsync(ct);

            return Results.Ok(snapshots);
        });
    }
}

public sealed record ProfileScoresDto(
    int? Vocabulary,
    int? Reading,
    int? Writing,
    int? Spelling,
    int Overall,
    string DifficultyBucket,
    string? CefrDisplay,
    DateTimeOffset? UpdatedAt)
{
    public static ProfileScoresDto From(Domain.Models.UserProfileScores scores) =>
        new(
            scores.Vocabulary,
            scores.Reading,
            scores.Writing,
            scores.Spelling,
            scores.Overall,
            scores.DifficultyBucket,
            scores.CefrDisplay,
            scores.UpdatedAt);
}

public sealed record ProfileScoreSnapshotDto(DateOnly Date, string ScoresJson);
