using Microsoft.Extensions.Caching.Memory;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Domain.Services;
using System.Security.Cryptography;
using System.Text;

namespace NextWord.Api.Endpoints;

public static class LlmEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/llm").WithTags("LLM");

        // T-064：LLM 配置状态——前端首次测评前据此判断是否需要先配置 API Key
        // llmMode: user-key（用户已配 BYOK）| server（服务端已启用真实 LLM）| mock（无可用真实 LLM）
        group.MapGet("/status", async (HttpContext http, IUserRepository users, LlmOpenAiOptions openAiOptions, CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(http, null, users, ct);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            var settings = await users.GetLlmSettingsAsync(user.Id, ct);
            var userHasApiKey = settings is not null && !string.IsNullOrWhiteSpace(settings.ApiKey);
            var serverEnabled = openAiOptions.Enabled && !string.IsNullOrWhiteSpace(openAiOptions.ApiKey);
            var mode = userHasApiKey ? "user-key" : serverEnabled ? "server" : "mock";
            return Results.Ok(new LlmStatusDto(mode, userHasApiKey, serverEnabled));
        });

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
/// <summary>T-064：LLM 配置状态（首次测评前配置引导用）。</summary>
public sealed record LlmStatusDto(string LlmMode, bool UserHasApiKey, bool ServerEnabled);

