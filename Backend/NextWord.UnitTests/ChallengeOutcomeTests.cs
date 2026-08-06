using System.Text.Json;
using Microsoft.Extensions.Options;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Domain.Services;
using NextWord.Infrastructure.Data;
using NextWord.Infrastructure.Repositories;
using NextWord.Infrastructure.Services;

namespace NextWord.UnitTests;

/// <summary>
/// T-035：挑战「有结果化」（DESIGN-challenge-outcome §4 验收）。
/// - Daily 通过：规则点评（最长/短板）+ 累计通过计数；未通过无点评；
/// - 阅读 1→3 题：正确率映射 0/33/67/100，阈值 67（答对 2 题过线）；
/// - ChallengeSession 结构兼容：旧单题会话 JSON（无 readings）+ 旧客户端单题作答不炸。
/// </summary>
public class ChallengeOutcomeTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // ── §4.1 规则点评（纯函数）──────────────────────────────

    [Fact]
    public void Feedback_points_out_strongest_and_weakest_dimensions()
    {
        var comment = ChallengeFeedback.Build(90, 60, 40, null);

        Assert.Contains("词汇", comment);
        Assert.Contains("最长板", comment);
        Assert.Contains("阅读", comment);
        Assert.Contains("提升空间", comment);
    }

    [Fact]
    public void Feedback_compares_against_profile_baseline()
    {
        var profile = new UserProfileScores(60, 70, 50, null, 60, "Intermediate", "B1", null);

        // 写作 85 vs 画像 50 → 高出一截；阅读 40 vs 画像 70 → 拖了后腿
        var comment = ChallengeFeedback.Build(60, 85, 40, profile);

        Assert.Contains("写作比你的当前水平高出一截", comment);
        Assert.Contains("阅读拖了后腿", comment);
    }

    // ── §4.3 阅读 3 题计分与阈值（真实 PG + 固定挑战包）──────

    [Fact]
    public async Task Two_of_three_reading_correct_scores_67_and_passes()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db);
        var service = CreateChallengeService(db, out _);

        var first = await RunDailyChallengeAsync(db, service, user.Id, readingAnswers: [0, 9, 2]);
        Assert.True(first.Passed);
        Assert.Equal(67, first.ReadingScore);
        Assert.NotNull(first.Feedback);
        Assert.Equal(1, first.PassCount);

        // 通过计数从 ChallengeRecords 派生：第二次通过 +1
        var second = await RunDailyChallengeAsync(db, service, user.Id, readingAnswers: [0, 1, 2]);
        Assert.True(second.Passed);
        Assert.Equal(100, second.ReadingScore);
        Assert.Equal(2, second.PassCount);
    }

    [Fact]
    public async Task One_of_three_reading_correct_scores_33_and_fails()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db);
        var service = CreateChallengeService(db, out _);

        var result = await RunDailyChallengeAsync(db, service, user.Id, readingAnswers: [0, 9, 9]);

        Assert.False(result.Passed);
        Assert.Equal(33, result.ReadingScore);
        // 未通过：无点评，有计数（0 次通过）
        Assert.Null(result.Feedback);
        Assert.Equal(0, result.PassCount);
    }

    [Fact]
    public async Task Legacy_single_reading_session_still_scores()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db);
        var service = CreateChallengeService(db, out var pack);

        // 旧格式会话 JSON：只有单题 reading，没有 readings 属性
        var legacyJson = JsonSerializer.Serialize(new
        {
            vocabulary = pack.Vocabulary,
            sentence = pack.Sentence,
            reading = pack.Reading,
            attemptedLevel = pack.AttemptedLevel,
        }, JsonOptions);
        var session = new ChallengeSession
        {
            UserId = user.Id,
            PackJson = legacyJson,
            ConfirmationChallenge = false,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(2),
        };
        db.ChallengeSessions.Add(session);
        await db.SaveChangesAsync();

        // 旧客户端：单题 readingSelectedIndex
        var result = await service.SubmitChallengeAsync(
            user.Id,
            new ChallengeSubmitRequest(
                session.Id,
                ChallengeType.Daily,
                [0, 0, 0, 0, 0],
                "I want to achieve my goal.",
                "achieve",
                "academic",
                null,
                pack.Reading.CorrectIndex,
                0),
            CancellationToken.None);

        Assert.Equal(100, result.ReadingScore);
        Assert.True(result.Passed);
    }

    // ── §4.3 出题：3 篇/3 题，考点词出自正文（真实 PG）──────

    [Fact]
    public async Task Pack_generator_returns_three_reading_questions_from_library()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        // 共享测试库约定：播种选 C2 档（测评选带封顶 C1），避免污染其他测试的带内词池
        var lemmas = new[] { "harvest", "lantern", "meadow" };
        foreach (var lemma in lemmas)
        {
            db.Articles.Add(new Article
            {
                Title = $"Article about {lemma}",
                Content = $"The {lemma} is central to this short story. " +
                    $"People talk about the {lemma} every season. " +
                    $"This passage explains why the {lemma} matters to the village.",
                CefrLevel = CefrLevel.C2,
                WordCount = 30,
            });
        }

        for (var i = 0; i < 6; i++)
        {
            db.Words.Add(new Word
            {
                Lemma = i < lemmas.Length ? lemmas[i] : $"filler{i}",
                Meanings = [$"含义{i}"],
                CefrLevel = CefrLevel.C2,
                Utility = WordUtility.High,
            });
        }

        await db.SaveChangesAsync();

        var generator = new ChallengePackGenerator(db);
        // 确认挑战按当前档出题：目标档 = C2，恰好命中上面播种的词与文
        var pack = await generator.GenerateAsync(Guid.NewGuid(), CefrLevel.C2, true, CancellationToken.None);

        Assert.Equal(CefrLevel.C2, pack.AttemptedLevel);
        var readings = Assert.IsAssignableFrom<IReadOnlyList<ReadingQuizQuestion>>(pack.Readings);
        Assert.Equal(3, readings.Count);
        foreach (var question in readings)
        {
            Assert.Equal(4, question.Options.Count);
            Assert.InRange(question.CorrectIndex, 0, question.Options.Count - 1);
            // 考点词口径：问题中的考点词必须出现在摘要正文里
            var lemma = question.Question.Split('"')[1];
            Assert.Contains(lemma.ToLowerInvariant(), question.ArticleExcerpt.ToLowerInvariant());
        }
    }

    // ── 装配与播种 ──────────────────────────────────────────

    private static async Task<User> SeedUserAsync(ApplicationDbContext db)
    {
        var user = new User { DisplayName = $"t035-{Guid.NewGuid():N}" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    /// <summary>固定挑战包：词汇 5 题正确项都在 0；阅读 3 题正确项依次 0/1/2。</summary>
    private static ChallengePack FixedPack()
    {
        var vocabulary = Enumerable.Range(0, 5)
            .Select(i => new VocabQuizQuestion($"word{i}", ["正确", "错1", "错2", "错3"], 0, DifficultyLevel.Basic))
            .ToList();
        var readings = Enumerable.Range(0, 3)
            .Select(i => new ReadingQuizQuestion(
                Guid.NewGuid(),
                $"文中 \"key{i}\" 的含义是什么？",
                ["甲", "乙", "丙", "丁"],
                i,
                $"excerpt with key{i} repeated several times for length"))
            .ToList();
        return new ChallengePack(
            vocabulary,
            new SentenceQuizQuestion(null, "achieve", "academic"),
            readings[0],
            CefrLevel.B1)
        { Readings = readings };
    }

    private static ChallengeService CreateChallengeService(ApplicationDbContext db, out ChallengePack pack)
    {
        pack = FixedPack();
        var scoreProfile = new ScoreProfileService(db, new ScoreMappingService(new ScoreMappingOptions()));
        return new ChallengeService(
            db,
            new StubPackGenerator(pack),
            new LevelUpgradeEngine(),
            new UserRepository(db),
            scoreProfile,
            new AssessmentScoringService(new ScoreMappingOptions()),
            new SentenceService(
                db,
                new StubLlmFactory(new NeutralRatingLlm()),
                Options.Create(new LlmSentenceRatingOptions()),
                new LearningPlanService(db, scoreProfile),
                scoreProfile),
            new StubEvaluationReports(),
            Options.Create(new ChallengeThresholdsOptions()));
    }

    /// <summary>跑一次 Daily 挑战：词汇全对（答案 0），造句走中性评分（写作 60），阅读按给定作答。</summary>
    private static async Task<ChallengeSubmitResponse> RunDailyChallengeAsync(
        ApplicationDbContext db,
        ChallengeService service,
        Guid userId,
        int[] readingAnswers)
    {
        var start = await service.StartChallengeAsync(userId, false, CancellationToken.None);
        return await service.SubmitChallengeAsync(
            userId,
            new ChallengeSubmitRequest(
                start.ChallengeSessionId,
                ChallengeType.Daily,
                [0, 0, 0, 0, 0],
                "I want to achieve my goal.",
                "achieve",
                "academic",
                null,
                null,
                0)
            { ReadingSelectedIndexes = readingAnswers },
            CancellationToken.None);
    }

    private sealed class StubPackGenerator(ChallengePack pack) : IChallengePackGenerator
    {
        public Task<ChallengePack> GenerateAsync(Guid userId, CefrLevel currentLevel, bool confirmationChallenge, CancellationToken cancellationToken)
            => Task.FromResult(pack);
    }

    private sealed class StubEvaluationReports : IEvaluationReportService
    {
        public Task<long> EnqueueForUserAsync(Guid userId, string triggerType, Guid? assessmentId, CancellationToken cancellationToken)
            => Task.FromResult(0L);
        public Task ProcessJobAsync(BackgroundJob job, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubLlmFactory(ILLMProvider provider) : IUserLlmProviderFactory
    {
        public Task<ILLMProvider> GetForUserAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(provider);
    }

    /// <summary>固定中性评分 3/3/3/3 → 写作 60，稳过 WritingScoreMin 53。</summary>
    private sealed class NeutralRatingLlm : ILLMProvider
    {
        public Task<SentenceRatingResponse> RateSentenceAsync(SentenceRatingRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new SentenceRatingResponse(
                3, 3, 3, 3, "B", string.Empty, [], DifficultyLevel.Intermediate, string.Empty));

        public Task<DifficultyRating> RateDifficultyAsync(ItemRatingRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<DefinitionResponse> GetDefinitionAsync(DefinitionRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<VocabExtractResponse> ExtractVocabAsync(VocabExtractRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<CommentReplyResponse> ReplyToCommentAsync(CommentReplyRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ScenarioAnnotationResponse> AnnotateScenarioAsync(ScenarioAnnotationRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<WeaknessProfileResponse> GenerateWeaknessProfileAsync(WeaknessProfileRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<BottleneckInsightResponse> GenerateBottleneckInsightAsync(BottleneckInsightRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
