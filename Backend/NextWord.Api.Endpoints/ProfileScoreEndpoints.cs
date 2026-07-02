using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Domain.Services;

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
    public static ProfileScoresDto From(UserProfileScores scores) =>
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
