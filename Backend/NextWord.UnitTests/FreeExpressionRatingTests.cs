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
/// T-037：自由表达评分不再把字面量「free expression」当 targetWord 传给 LLM
/// （qwen-plus 把它当写作主题，高质量段落被判 off-topic 拿 C）。
/// - 请求侧：传中性主题描述 + IsFreeExpression 标记；
/// - prompt 侧：自由表达专门变体，无 Target Word 行，相关性按「围绕日常场景/主题」评；
/// - Mock 侧：自由表达不扣「未用目标词」。
/// </summary>
public class FreeExpressionRatingTests
{
    [Fact]
    public async Task Free_expression_request_drops_literal_target_word()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = new User { DisplayName = $"t037-{Guid.NewGuid():N}" };
        db.Users.Add(user);
        db.UserProgress.Add(new UserProgress
        {
            UserId = user.Id,
            VocabularyScore = 60,
            ReadingScore = 60,
            WritingScore = 60,
            CefrDisplay = "B1"
        });
        await db.SaveChangesAsync();

        var llm = new CapturingRatingLlm();
        var service = new FreeExpressionService(
            db,
            new StubLlmFactory(llm),
            Options.Create(new LlmSentenceRatingOptions()),
            new ScoreProfileService(db, new ScoreMappingService(new ScoreMappingOptions())));

        await service.RateAsync(user.Id, "Today I cooked dinner for my family and we talked about our plans.", null, CancellationToken.None);

        var request = Assert.IsType<SentenceRatingRequest>(llm.LastRequest);
        Assert.True(request.IsFreeExpression);
        Assert.DoesNotContain("free expression", request.TargetWord, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("free expression", request.Scene, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Free_expression_prompt_has_no_target_word_line()
    {
        var prompt = LlmPromptFactory.BuildSentenceRatingPrompt(new SentenceRatingRequest(
            "Today I cooked dinner for my family.",
            "日常自由表达",
            "daily-life",
            "B1",
            IsFreeExpression: true));

        Assert.DoesNotContain("Target Word", prompt);
        Assert.Contains("no assigned topic word", prompt);
        Assert.Contains("relevance_score", prompt);
        // T-027 挑战度规则对自由表达同样适用
        Assert.Contains("挑战度", prompt);
        Assert.Contains("overall_grade 最高为 B", prompt);
    }

    [Fact]
    public void Sentence_rating_prompt_unchanged_for_target_word_tasks()
    {
        var prompt = LlmPromptFactory.BuildSentenceRatingPrompt(
            new SentenceRatingRequest("I eat healthy food.", "healthy", "life", "B2"));

        Assert.Contains("Target Word: healthy", prompt);
        Assert.Contains("挑战度", prompt);
    }

    /// <summary>qwen 误判场景的 Mock 回归：高质量自由表达段落不因「没用某个词」被压相关性/词汇维。</summary>
    [Fact]
    public async Task Mock_free_expression_does_not_penalize_missing_target_word()
    {
        var provider = new LlmMockProvider(new FixedModelProfileResolver());
        var response = await provider.RateSentenceAsync(
            new SentenceRatingRequest(
                "Although I was tired after work, I still cooked dinner for my family because we enjoy talking together.",
                "日常自由表达",
                "daily-life",
                "B1",
                IsFreeExpression: true),
            CancellationToken.None);

        Assert.True(response.RelevanceScore >= 4, $"自由表达相关性不应按「用没用某词」扣，实际 {response.RelevanceScore}");
        Assert.True(response.VocabularyScore >= 4, $"自由表达词汇维不应按「用没用某词」扣，实际 {response.VocabularyScore}");
        Assert.Contains(response.OverallGrade, new[] { "A", "B" });
        Assert.DoesNotContain(response.ErrorAnalysis, item => item.Contains("目标词"));
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
