using Microsoft.Extensions.Options;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Domain.Services;
using NextWord.Infrastructure.Services;

namespace NextWord.UnitTests;

/// <summary>T-049：阅读查词降级可见——降级内容（Mock 占位 / LLM 失败回退）响应 Offline=true。</summary>
public sealed class ReadingLookupOfflineTests
{
    [Fact]
    public async Task LookupAsync_FallbackDefinitionIsMarkedOffline()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var service = CreateService(db, isFallback: true);

        var response = await service.LookupAsync(
            Guid.NewGuid(),
            new ReadingLookupRequest("cart", "We bought ice cream from a cart.", Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(response.Offline);
        Assert.StartsWith("[离线模式]", response.ContextDefinition);
        Assert.False(response.FromCache);
    }

    [Fact]
    public async Task LookupAsync_RealDefinitionIsNotOffline()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var service = CreateService(db, isFallback: false);

        var response = await service.LookupAsync(
            Guid.NewGuid(),
            new ReadingLookupRequest("cart", "We bought ice cream from a cart.", Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(response.Offline);
        Assert.Equal("mock definition", response.ContextDefinition);
    }

    private static ReadingLookupService CreateService(Infrastructure.Data.ApplicationDbContext db, bool isFallback)
    {
        var articleVocab = new StubArticleVocabService(isFallback);
        var factory = new FixedLlmFactory(new StubLlmProvider(isFallback));
        var options = Options.Create(new LlmSentenceRatingOptions { ExplanationLanguage = "zh-CN" });
        return new ReadingLookupService(db, articleVocab, factory, options);
    }

    private static DefinitionResponse BuildDefinition(DefinitionRequest request, bool isFallback) => new(
        request.Word,
        "/mock/",
        [new Meaning("mock definition", true, request.Context ?? string.Empty)],
        [],
        [new WordExample(WordExampleKind.Contextual, "Mock sentence.", "mock explanation")],
        "mock usage",
        DifficultyLevel.Basic,
        CefrLevel.A2,
        IsFallback: isFallback);

    private sealed class StubArticleVocabService(bool isFallback) : IArticleVocabService
    {
        public Task<IReadOnlyList<ArticleVocabMapping>> GetMappingsAsync(Guid articleId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<ArticleVocabMapping>> ExtractAndPersistAsync(Guid articleId, Guid userId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<ArticleWordDetailResult> GetOrCreateWordDetailAsync(
            Guid articleId, Guid userId, string word, string? context, CancellationToken cancellationToken) =>
            Task.FromResult(new ArticleWordDetailResult(
                BuildDefinition(new DefinitionRequest(word, context, new LlmRequestOptions("reading-lookup", "reading_lookup"), "zh-CN"), isFallback),
                false));

        public Task<ArticleWordDetailResult> LookupWordAsync(
            Guid articleId, Guid userId, string word, string? context, CancellationToken cancellationToken) =>
            GetOrCreateWordDetailAsync(articleId, userId, word, context, cancellationToken);
    }

    private sealed class StubLlmProvider(bool isFallback) : ILLMProvider
    {
        public Task<DifficultyRating> RateDifficultyAsync(ItemRatingRequest request, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<DefinitionResponse> GetDefinitionAsync(DefinitionRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(BuildDefinition(request, isFallback));

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

    private sealed class FixedLlmFactory(ILLMProvider provider) : IUserLlmProviderFactory
    {
        public Task<ILLMProvider> GetForUserAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(provider);
    }
}
