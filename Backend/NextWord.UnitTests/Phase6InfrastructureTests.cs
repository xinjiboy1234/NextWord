using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Infrastructure.Caching;
using NextWord.Infrastructure.Services;

namespace NextWord.UnitTests;

public sealed class RedisCacheServiceTests
{
    [Fact]
    public async Task SetAndGet_RoundTripsValue()
    {
        var cache = new RedisCacheService(new InMemoryDistributedCache());

        await cache.SetAsync("test-key", new SamplePayload("hello", 42), TimeSpan.FromMinutes(1));
        var result = await cache.GetAsync<SamplePayload>("test-key");

        Assert.NotNull(result);
        Assert.Equal("hello", result!.Name);
        Assert.Equal(42, result.Count);
    }

    [Fact]
    public async Task RemoveAsync_DeletesKey()
    {
        var cache = new RedisCacheService(new InMemoryDistributedCache());

        await cache.SetAsync("remove-me", 1, TimeSpan.FromMinutes(1));
        await cache.RemoveAsync("remove-me");
        var result = await cache.GetAsync<int>("remove-me");

        Assert.Equal(default, result);
    }

    private sealed record SamplePayload(string Name, int Count);
}

public sealed class LlmTelemetryProviderTests
{
    [Fact]
    public async Task RateDifficultyAsync_DelegatesToInnerProvider()
    {
        var inner = new StubLlmProvider();
        var telemetry = new LlmTelemetryProvider(inner, NullLogger<LlmTelemetryProvider>.Instance);

        var result = await telemetry.RateDifficultyAsync(
            new ItemRatingRequest(ItemType.Word, "test", new LlmRequestOptions("test-profile", "difficulty_rating")),
            CancellationToken.None);

        Assert.Equal("test-profile", result.ModelProfileId);
        Assert.Equal(1, inner.CallCount);
    }

    private sealed class StubLlmProvider : ILLMProvider
    {
        public int CallCount { get; private set; }

        public Task<DifficultyRating> RateDifficultyAsync(ItemRatingRequest request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new DifficultyRating(
                request.ItemType,
                DifficultyLevel.Basic,
                CefrLevel.A1,
                "ok",
                RecommendedAction.LearnNow,
                0.9,
                request.Options?.ModelProfileId ?? "local-dev"));
        }

        public Task<DefinitionResponse> GetDefinitionAsync(DefinitionRequest request, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<SentenceRatingResponse> RateSentenceAsync(SentenceRatingRequest request, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<VocabExtractResponse> ExtractVocabAsync(VocabExtractRequest request, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<CommentReplyResponse> ReplyToCommentAsync(CommentReplyRequest request, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<ScenarioAnnotationResponse> AnnotateScenarioAsync(ScenarioAnnotationRequest request, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<WeaknessProfileResponse> GenerateWeaknessProfileAsync(WeaknessProfileRequest request, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<BottleneckInsightResponse> GenerateBottleneckInsightAsync(BottleneckInsightRequest request, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }
}

internal sealed class InMemoryDistributedCache : IDistributedCache
{
    private readonly Dictionary<string, byte[]> _store = new(StringComparer.Ordinal);

    public byte[]? Get(string key) => _store.GetValueOrDefault(key);

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        => Task.FromResult(Get(key));

    public void Refresh(string key) { }

    public Task RefreshAsync(string key, CancellationToken token = default)
        => Task.CompletedTask;

    public void Remove(string key) => _store.Remove(key);

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        Remove(key);
        return Task.CompletedTask;
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        => _store[key] = value;

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        Set(key, value, options);
        return Task.CompletedTask;
    }
}
