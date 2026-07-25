using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Domain.Services;
using NextWord.Infrastructure.Data;
using NextWord.Infrastructure.Services;

namespace NextWord.UnitTests;

public sealed class ArticleVocabCacheTests
{
    [Fact]
    public async Task GetOrCreateWordDetailAsync_ReturnsCachedMappingWithoutCallingLlm()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var articleId = await SeedArticleAsync(db);
        db.ArticleVocabMappings.Add(new ArticleVocabMapping
        {
            ArticleId = articleId,
            WordLemma = "cart",
            ContextMeaning = "流动售货车",
            Phonetics = "/kɑːrt/",
            ExamplesJson = WordExampleJson.Serialize(
            [
                new WordExample(WordExampleKind.Contextual, "We bought ice cream from a cart.", "文中用法")
            ]),
            DifficultyInContext = DifficultyLevel.Basic,
            IsKeyVocab = true
        });
        await db.SaveChangesAsync();

        var llm = new TrackingLlmProvider();
        var service = CreateService(db, llm);

        var result = await service.GetOrCreateWordDetailAsync(articleId, Guid.NewGuid(), "cart", "from a cart", CancellationToken.None);

        Assert.True(result.FromCache);
        Assert.Equal("流动售货车", result.Definition.Meanings[0].Definition);
        Assert.Equal(0, llm.DefinitionCalls);
    }

    [Fact]
    public async Task GetOrCreateWordDetailAsync_PersistsLlmResultOnMiss()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var articleId = await SeedArticleAsync(db);
        var llm = new TrackingLlmProvider();
        var service = CreateService(db, llm);

        var first = await service.GetOrCreateWordDetailAsync(articleId, Guid.NewGuid(), "cart", "from a cart", CancellationToken.None);
        var second = await service.GetOrCreateWordDetailAsync(articleId, Guid.NewGuid(), "cart", "from a cart", CancellationToken.None);

        Assert.False(first.FromCache);
        Assert.True(second.FromCache);
        Assert.Equal(1, llm.DefinitionCalls);
        var stored = await db.ArticleVocabMappings.SingleAsync(mapping => mapping.ArticleId == articleId && mapping.WordLemma == "cart");
        Assert.False(string.IsNullOrWhiteSpace(stored.Phonetics));
        Assert.NotNull(stored.ExamplesJson);
    }

    [Fact]
    public async Task GetOrCreateWordDetailAsync_BackfillsLegacyMappingWithoutOverwritingContextMeaning()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var articleId = await SeedArticleAsync(db);
        db.ArticleVocabMappings.Add(new ArticleVocabMapping
        {
            ArticleId = articleId,
            WordLemma = "cart",
            ContextMeaning = "保留的文中含义",
            SpecialUsage = "旧提示",
            DifficultyInContext = DifficultyLevel.Basic,
            IsKeyVocab = true
        });
        await db.SaveChangesAsync();

        var llm = new TrackingLlmProvider();
        var service = CreateService(db, llm);

        var result = await service.GetOrCreateWordDetailAsync(articleId, Guid.NewGuid(), "cart", "from a cart", CancellationToken.None);

        Assert.False(result.FromCache);
        Assert.Equal(1, llm.DefinitionCalls);
        var stored = await db.ArticleVocabMappings.SingleAsync(mapping => mapping.ArticleId == articleId);
        Assert.Equal("保留的文中含义", stored.ContextMeaning);
        Assert.False(string.IsNullOrWhiteSpace(stored.Phonetics));
        Assert.NotNull(stored.ExamplesJson);
    }

    private static async Task<Guid> SeedArticleAsync(ApplicationDbContext db)
    {
        var article = new Article
        {
            Title = "Test Article",
            Content = "Sometimes we buy ice cream from a cart.",
            DifficultyLevel = DifficultyLevel.Basic,
            CefrLevel = CefrLevel.A2,
            WordCount = 8,
            Source = ArticleSource.Builtin
        };
        db.Articles.Add(article);
        await db.SaveChangesAsync();
        return article.Id;
    }

    private static ArticleVocabService CreateService(ApplicationDbContext db, ILLMProvider llm)
    {
        var factory = new FixedLlmFactory(llm);
        var users = new FixedUserRepository();
        var options = Microsoft.Extensions.Options.Options.Create(new LlmSentenceRatingOptions { ExplanationLanguage = "zh-CN" });
        return new ArticleVocabService(db, factory, users, options);
    }

    private sealed class TrackingLlmProvider : ILLMProvider
    {
        public int DefinitionCalls { get; private set; }

        public Task<DifficultyRating> RateDifficultyAsync(ItemRatingRequest request, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<DefinitionResponse> GetDefinitionAsync(DefinitionRequest request, CancellationToken cancellationToken)
        {
            DefinitionCalls++;
            return Task.FromResult(new DefinitionResponse(
                request.Word,
                "/mock/",
                [new Meaning("mock definition", true, request.Context ?? string.Empty)],
                [],
                [new WordExample(WordExampleKind.Contextual, "Mock sentence.", "mock explanation")],
                "mock usage",
                DifficultyLevel.Basic,
                CefrLevel.A2));
        }

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

    private sealed class FixedUserRepository : IUserRepository
    {
        public Task<User> GetOrCreateDefaultUserAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new User { Id = Guid.NewGuid(), DisplayName = "tester" });

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<User?>(new User { Id = id, DisplayName = "tester" });

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult<User?>(null);

        public Task<User> CreateUserAsync(string email, string passwordHash, string displayName, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<UserLlmSettings?> GetLlmSettingsAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<UserLlmSettings?>(null);

        public Task<UserLlmSettings> UpsertLlmSettingsAsync(UserLlmSettings settings, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<UserWordRelationship> GetOrCreateRelationshipAsync(Guid userId, Guid wordId, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<UserProgress> GetOrCreateProgressAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(new UserProgress { UserId = userId, ReadingLevel = CefrLevel.A2 });

        public Task AddLearningLogAsync(WordLearningLog log, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
