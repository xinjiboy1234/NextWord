using Microsoft.Extensions.Options;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Domain.Services;
using NextWord.Infrastructure.Data;
using NextWord.Infrastructure.Services;

namespace NextWord.UnitTests;

/// <summary>
/// T-027：造句/自由表达评分纳入「相对水平的挑战度」。
/// - 评分尺子 = 用户当前水平带（UserProgress 投影，ScoreMapping 单一来源；无进度回退调用方传入带，再退默认带）；
/// - Mock 口径：句长/连接词数明显低于带期望的「安全简单句」压词汇维（≤3）、总评封顶 B；
///   与水平带相称的简单句不受影响（菜鸟公平性，prompted_use 确认链路依赖 A/B 档）。
/// </summary>
public class RatingChallengeTests
{
    // ── Mock 挑战度口径（无 DB）──────────────────────────────

    [Fact]
    public async Task Mock_caps_safe_simple_sentence_for_b2_user()
    {
        var provider = new LlmMockProvider(new FixedModelProfileResolver());
        var response = await provider.RateSentenceAsync(
            new SentenceRatingRequest("It's healthy. Moreover, it's super cheap.", "healthy", "life", "B2"),
            CancellationToken.None);

        Assert.True(response.VocabularyScore <= 3, $"B2 安全简单句词汇维应 ≤3，实际 {response.VocabularyScore}");
        Assert.NotEqual("A", response.OverallGrade);
        Assert.Contains(response.OverallGrade, new[] { "B", "C", "D" });
    }

    [Fact]
    public async Task Mock_keeps_level_appropriate_simple_sentence_for_a2_user()
    {
        var provider = new LlmMockProvider(new FixedModelProfileResolver());
        var response = await provider.RateSentenceAsync(
            new SentenceRatingRequest("I eat healthy food every day.", "healthy", "life", "A2"),
            CancellationToken.None);

        // A2 用户写与水平相称的简单句仍可拿 B 及以上——不一棍子打死简单句
        Assert.Contains(response.OverallGrade, new[] { "A", "B" });
    }

    [Fact]
    public async Task Mock_does_not_penalize_challenging_sentence_for_b2_user()
    {
        var provider = new LlmMockProvider(new FixedModelProfileResolver());
        var response = await provider.RateSentenceAsync(
            new SentenceRatingRequest(
                "Although healthy food is often expensive, I still buy it because it keeps me energetic.",
                "healthy", "life", "B2"),
            CancellationToken.None);

        Assert.True(response.VocabularyScore >= 4, $"带内挑战句词汇维不应被压，实际 {response.VocabularyScore}");
        Assert.Contains(response.OverallGrade, new[] { "A", "B" });
    }

    // ── 评分带传递（真实 PG + 捕获桩）────────────────────────

    [Fact]
    public async Task Sentence_rating_uses_band_from_user_progress()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, cefrDisplay: "B2", withScores: true);
        var llm = new CapturingRatingLlm();
        var service = CreateSentenceService(db, llm);

        // 客户端不传/传错 userLevel 都不影响：尺子以 UserProgress 投影为准
        await service.RateAsync(user.Id, null, "achieve", "I want to achieve my goal.", "life", null, CancellationToken.None);

        Assert.Equal("B2", llm.LastRequest?.UserLevel);
    }

    [Fact]
    public async Task Sentence_rating_falls_back_without_progress()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var llm = new CapturingRatingLlm();
        var service = CreateSentenceService(db, llm);

        // 无进度：回退调用方显式传入的带（测评/挑战路径）
        var assessed = await SeedUserAsync(db, cefrDisplay: null, withScores: false);
        await service.RateAsync(assessed.Id, null, "achieve", "I want to achieve my goal.", "assessment", "B1", CancellationToken.None);
        Assert.Equal("B1", llm.LastRequest?.UserLevel);

        // 无进度且未传带：回退默认带 A2（匿名/新用户）
        var anonymous = await SeedUserAsync(db, cefrDisplay: null, withScores: false);
        await service.RateAsync(anonymous.Id, null, "achieve", "I want to achieve my goal.", "life", null, CancellationToken.None);
        Assert.Equal("A2", llm.LastRequest?.UserLevel);
    }

    [Fact]
    public async Task Free_expression_rating_uses_band_from_user_progress()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, cefrDisplay: "C1", withScores: true);
        var llm = new CapturingRatingLlm();
        var service = new FreeExpressionService(
            db,
            new StubLlmFactory(llm),
            Options.Create(new LlmSentenceRatingOptions()),
            CreateScoreProfile(db));

        await service.RateAsync(user.Id, "I write whatever comes to my mind tonight.", null, CancellationToken.None);

        Assert.Equal("C1", llm.LastRequest?.UserLevel);
    }

    // ── Prompt 挑战度规则（解析器不变，仅断言指令在）──────────

    [Fact]
    public void Sentence_rating_prompt_states_challenge_rules()
    {
        var prompt = LlmPromptFactory.BuildSentenceRatingPrompt(
            new SentenceRatingRequest("I eat healthy food.", "healthy", "life", "B2"));

        Assert.Contains("挑战度", prompt);
        Assert.Contains("水平带", prompt);
        Assert.Contains("vocabulary_score 不超过 3", prompt);
        Assert.Contains("overall_grade 最高为 B", prompt);
    }

    // ── 装配与播种 ───────────────────────────────────────────

    private static SentenceService CreateSentenceService(ApplicationDbContext db, ILLMProvider llm)
    {
        var scoreProfile = CreateScoreProfile(db);
        return new SentenceService(
            db,
            new StubLlmFactory(llm),
            Options.Create(new LlmSentenceRatingOptions()),
            new LearningPlanService(db, scoreProfile),
            scoreProfile);
    }

    private static ScoreProfileService CreateScoreProfile(ApplicationDbContext db)
        => new(db, new ScoreMappingService(new ScoreMappingOptions()));

    private static async Task<User> SeedUserAsync(ApplicationDbContext db, string? cefrDisplay, bool withScores)
    {
        var user = new User { DisplayName = $"t027-{Guid.NewGuid():N}" };
        db.Users.Add(user);
        db.UserProgress.Add(new UserProgress
        {
            UserId = user.Id,
            VocabularyScore = withScores ? 72 : null,
            ReadingScore = withScores ? 72 : null,
            WritingScore = withScores ? 72 : null,
            CefrDisplay = cefrDisplay
        });
        await db.SaveChangesAsync();
        return user;
    }

    private sealed class FixedModelProfileResolver : IModelProfileResolver
    {
        public ModelProfile Resolve(string? profileId) => new() { Id = profileId ?? "local-dev" };
    }

    private sealed class StubLlmFactory(ILLMProvider provider) : IUserLlmProviderFactory
    {
        public Task<ILLMProvider> GetForUserAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(provider);
    }

    /// <summary>捕获评分入参的桩：返回固定中性评分，其余方法不允许被调用。</summary>
    private sealed class CapturingRatingLlm : ILLMProvider
    {
        public SentenceRatingRequest? LastRequest { get; private set; }

        public Task<SentenceRatingResponse> RateSentenceAsync(SentenceRatingRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new SentenceRatingResponse(
                3, 3, 3, 3, "B", string.Empty, [], DifficultyLevel.Intermediate, string.Empty));
        }

        public Task<DifficultyRating> RateDifficultyAsync(ItemRatingRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<DefinitionResponse> GetDefinitionAsync(DefinitionRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<VocabExtractResponse> ExtractVocabAsync(VocabExtractRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<CommentReplyResponse> ReplyToCommentAsync(CommentReplyRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ScenarioAnnotationResponse> AnnotateScenarioAsync(ScenarioAnnotationRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<WeaknessProfileResponse> GenerateWeaknessProfileAsync(WeaknessProfileRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<BottleneckInsightResponse> GenerateBottleneckInsightAsync(BottleneckInsightRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
