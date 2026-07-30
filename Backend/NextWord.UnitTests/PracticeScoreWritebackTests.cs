using Microsoft.EntityFrameworkCore;
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
/// T-022：日常造句/自由表达评分小步回写 Writing 维（真实 PG）。
/// 口径：observed = MapSentenceToScore(四维均分)；delta = clamp(round((observed - current) * 0.1), -2, +2)；
/// 幂等键 sentence-score:{logId} / freeexpr-score:{logId}，delta=0 也落幂等记录防重放；
/// 测评/挑战路径直接调 SentenceService.RateAsync（不经过端点回写），不产生 sentence-score 事件。
/// </summary>
public class PracticeScoreWritebackTests
{
    [Fact]
    public async Task Sentence_observed_above_current_raises_writing_clamped_to_plus_2()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, writing: 50);
        var writeback = CreateWriteback(db);

        // 四维全 5 → observed = 100；raw delta = round(50 * 0.1) = 5 → clamp 到 +2
        var log = SentenceLog(user.Id, grammar: 5, natural: 5, vocabulary: 5, relevance: 5);
        var change = await writeback.ApplySentenceAsync(user.Id, log, CancellationToken.None);

        Assert.Equal(50, change.Before);
        Assert.Equal(52, change.After);
        var progress = await db.UserProgress.SingleAsync(item => item.UserId == user.Id);
        Assert.Equal(52, progress.WritingScore);
        Assert.Equal(1, await db.LearningEvents.CountAsync(item => item.IdempotencyKey == $"sentence-score:{log.Id}"));
    }

    [Fact]
    public async Task Sentence_observed_below_current_lowers_writing_clamped_to_minus_2()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, writing: 80);
        var writeback = CreateWriteback(db);

        // 四维全 1 → observed = 20；raw delta = round(-60 * 0.1) = -6 → clamp 到 -2
        var log = SentenceLog(user.Id, grammar: 1, natural: 1, vocabulary: 1, relevance: 1);
        var change = await writeback.ApplySentenceAsync(user.Id, log, CancellationToken.None);

        Assert.Equal(80, change.Before);
        Assert.Equal(78, change.After);
        var progress = await db.UserProgress.SingleAsync(item => item.UserId == user.Id);
        Assert.Equal(78, progress.WritingScore);
    }

    [Fact]
    public async Task Sentence_small_gap_moves_one_step()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, writing: 50);
        var writeback = CreateWriteback(db);

        // 四维均分 2.75 → observed = 55；raw delta = round(5 * 0.1) = 1（AwayFromZero）
        var log = SentenceLog(user.Id, grammar: 3, natural: 3, vocabulary: 3, relevance: 2);
        var change = await writeback.ApplySentenceAsync(user.Id, log, CancellationToken.None);

        Assert.Equal(50, change.Before);
        Assert.Equal(51, change.After);
    }

    [Fact]
    public async Task Same_idempotency_key_does_not_apply_twice()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, writing: 50);
        var writeback = CreateWriteback(db);
        var log = SentenceLog(user.Id, grammar: 5, natural: 5, vocabulary: 5, relevance: 5);

        var first = await writeback.ApplySentenceAsync(user.Id, log, CancellationToken.None);
        var second = await writeback.ApplySentenceAsync(user.Id, log, CancellationToken.None);

        Assert.Equal(52, first.After);
        // 重放：分数不再变化，幂等记录仍只有一条
        Assert.Equal(52, second.After);
        var progress = await db.UserProgress.SingleAsync(item => item.UserId == user.Id);
        Assert.Equal(52, progress.WritingScore);
        Assert.Equal(1, await db.LearningEvents.CountAsync(item => item.IdempotencyKey == $"sentence-score:{log.Id}"));
    }

    [Fact]
    public async Task Zero_delta_still_records_idempotency_event()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, writing: 60);
        var writeback = CreateWriteback(db);

        // 四维全 3 → observed = 60 = current → delta = 0，但幂等记录照常落库
        var log = SentenceLog(user.Id, grammar: 3, natural: 3, vocabulary: 3, relevance: 3);
        var change = await writeback.ApplySentenceAsync(user.Id, log, CancellationToken.None);

        Assert.Equal(60, change.Before);
        Assert.Equal(60, change.After);
        Assert.Equal(1, await db.LearningEvents.CountAsync(item => item.IdempotencyKey == $"sentence-score:{log.Id}"));
    }

    [Fact]
    public async Task Free_expression_writes_back_with_freeexpr_key()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, writing: 40);
        var writeback = CreateWriteback(db);

        // AiScore = 100（与 MapSentenceToScore(四维均分) 同口径）→ raw delta = 6 → clamp +2
        var log = new FreeExpressionLog { Id = Guid.NewGuid(), UserId = user.Id, UserText = "text", AiScore = 100 };
        var change = await writeback.ApplyFreeExpressionAsync(user.Id, log, CancellationToken.None);

        Assert.Equal(40, change.Before);
        Assert.Equal(42, change.After);
        Assert.Equal(1, await db.LearningEvents.CountAsync(item => item.IdempotencyKey == $"freeexpr-score:{log.Id}"));
    }

    [Fact]
    public async Task Assessment_path_rate_async_produces_no_sentence_score_event()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, writing: 50);

        // 测评/挑战路径直接调 SentenceService.RateAsync，不经过端点回写 → 无 sentence-score 事件
        var scoreProfile = CreateScoreProfile(db);
        var sentenceService = new SentenceService(
            db,
            new StubLlmFactory(new StaticRatingLlm(Rating(5))),
            Options.Create(new LlmSentenceRatingOptions()),
            new LearningPlanService(db, scoreProfile),
            scoreProfile);
        var log = await sentenceService.RateAsync(
            user.Id, null, "achieve", "I want to achieve my goal.", "assessment", "B1", CancellationToken.None);

        Assert.NotEqual(Guid.Empty, log.Id);
        Assert.Equal(0, await db.LearningEvents.CountAsync(item => item.UserId == user.Id && item.IdempotencyKey.StartsWith("sentence-score:")));
        Assert.Equal(0, await db.LearningEvents.CountAsync(item => item.UserId == user.Id && item.IdempotencyKey.StartsWith("freeexpr-score:")));
        var progress = await db.UserProgress.SingleAsync(item => item.UserId == user.Id);
        Assert.Equal(50, progress.WritingScore);
    }

    // ── 装配与播种 ───────────────────────────────────────────

    private static PracticeScoreWritebackService CreateWriteback(ApplicationDbContext db)
        => new(CreateScoreProfile(db), new AssessmentScoringService());

    private static ScoreProfileService CreateScoreProfile(ApplicationDbContext db)
        => new(db, new ScoreMappingService(new ScoreMappingOptions()));

    private static async Task<User> SeedUserAsync(ApplicationDbContext db, int writing)
    {
        var user = new User { DisplayName = $"t022-{Guid.NewGuid():N}" };
        db.Users.Add(user);
        db.UserProgress.Add(new UserProgress
        {
            UserId = user.Id,
            VocabularyScore = 50,
            ReadingScore = 50,
            WritingScore = writing,
            CefrDisplay = "B1"
        });
        await db.SaveChangesAsync();
        return user;
    }

    private static SentenceLog SentenceLog(Guid userId, int grammar, int natural, int vocabulary, int relevance) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        TargetWord = "achieve",
        Scene = "life",
        UserSentence = "I want to achieve my goal.",
        GrammarScore = grammar,
        NaturalScore = natural,
        VocabularyScore = vocabulary,
        RelevanceScore = relevance
    };

    private static SentenceRatingResponse Rating(int score) =>
        new(score, score, score, score, "A", string.Empty, [], DifficultyLevel.Intermediate, string.Empty);

    private sealed class StubLlmFactory(ILLMProvider provider) : IUserLlmProviderFactory
    {
        public Task<ILLMProvider> GetForUserAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(provider);
    }

    /// <summary>固定评分桩：只实现造句评分，其余方法不允许被调用。</summary>
    private sealed class StaticRatingLlm(SentenceRatingResponse rating) : ILLMProvider
    {
        public Task<SentenceRatingResponse> RateSentenceAsync(SentenceRatingRequest request, CancellationToken cancellationToken)
            => Task.FromResult(rating);

        public Task<DifficultyRating> RateDifficultyAsync(ItemRatingRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<DefinitionResponse> GetDefinitionAsync(DefinitionRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<VocabExtractResponse> ExtractVocabAsync(VocabExtractRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<CommentReplyResponse> ReplyToCommentAsync(CommentReplyRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ScenarioAnnotationResponse> AnnotateScenarioAsync(ScenarioAnnotationRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<WeaknessProfileResponse> GenerateWeaknessProfileAsync(WeaknessProfileRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<BottleneckInsightResponse> GenerateBottleneckInsightAsync(BottleneckInsightRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
