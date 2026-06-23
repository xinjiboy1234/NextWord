using Microsoft.Extensions.Caching.Memory;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using System.Security.Cryptography;
using System.Text;

namespace NextWord.Api.Endpoints;

public static class LlmEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/llm").WithTags("LLM");

        group.MapPost("/rate-difficulty", async (
            RateDifficultyRequest request,
            ILLMProvider llm,
            IMemoryCache cache,
            CancellationToken ct) =>
        {
            var itemType = request.ItemType ?? ItemType.Word;
            var cacheKey = $"llm:{itemType}:{Sha256(request.Text)}";
            if (cache.TryGetValue<DifficultyRating>(cacheKey, out var cached) && cached is not null)
            {
                return Results.Ok(cached with { Reason = $"{cached.Reason} Cache hit." });
            }

            var rating = await llm.RateDifficultyAsync(new ItemRatingRequest(
                itemType,
                request.Text,
                new LlmRequestOptions(request.ModelProfileId ?? "local-dev", "difficulty_rating")), ct);
            cache.Set(cacheKey, rating, TimeSpan.FromHours(24));
            return Results.Ok(rating);
        });
    }

    private static string Sha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim().ToLowerInvariant()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public sealed record RateDifficultyRequest(
    string Text,
    ItemType? ItemType,
    string? ModelProfileId);
