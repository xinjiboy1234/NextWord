using System.Text.Json;
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
/// T-014 词毕业四阶段生命周期（真实 PG + 纯规则状态机）：
/// 认识→回忆（SM-2 成熟阈值边界）；回忆→造句使用（回忆考察通过进候选池）；
/// 造句确认/使用错误回退；自发使用毕业留痕；指定目标词不算自发；
/// 自评只改 SM-2 排程、不改掌握度（对比断言）；Planner 候选池优先编排；每日词带阶段与考察模式。
/// </summary>
public class WordLifecycleTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // ── 状态机推进（纯规则，复用真实 Sm2Service 排程口径）────────────

    [Fact]
    public void Recognition_advances_to_recalled_only_at_sm2_maturity()
    {
        var sm2 = new Sm2Service();
        var relationship = new UserWordRelationship { UserId = Guid.NewGuid(), WordId = Guid.NewGuid() };
        var now = DateTimeOffset.UtcNow;

        // 第 1 次连续 Remembered：RepeatCount=1 未达成熟阈值 → 仍认识阶段
        sm2.ApplyReview(relationship, AssessmentResult.Remembered, now);
        WordLifecycleService.ApplyReview(relationship, WordQuizMode.Recognition, isCorrect: true, now);
        Assert.Equal(WordLifecycleStage.Recognized, relationship.LifecycleStage);
        Assert.Equal(25, relationship.MasteryScore);

        // 第 2 次连续 Remembered：RepeatCount=2 达到 SM-2 成熟阈值（interval 到 6 天）→ 回忆阶段
        sm2.ApplyReview(relationship, AssessmentResult.Remembered, now.AddDays(1));
        WordLifecycleService.ApplyReview(relationship, WordQuizMode.Recognition, isCorrect: true, now.AddDays(1));
        Assert.Equal(WordLifecycleStage.Recalled, relationship.LifecycleStage);
        Assert.Equal(50, relationship.MasteryScore);
        Assert.NotNull(relationship.StageUpdatedAt);
    }

    [Fact]
    public void Self_rating_changes_only_sm2_schedule_never_mastery()
    {
        var sm2 = new Sm2Service();
        var now = DateTimeOffset.UtcNow;

        // Forgot：SM-2 排程参数重置（RepeatCount 清零、interval 回 1），掌握度与阶段纹丝不动
        var forgotten = new UserWordRelationship
        {
            UserId = Guid.NewGuid(),
            WordId = Guid.NewGuid(),
            RepeatCount = 1,
            IntervalDays = 6,
            MasteryScore = WordLifecycleService.MasteryForStage(WordLifecycleStage.Recognized)
        };
        var masteryBefore = forgotten.MasteryScore;
        sm2.ApplyReview(forgotten, AssessmentResult.Forgot, now);
        WordLifecycleService.ApplyReview(forgotten, WordQuizMode.Recognition, isCorrect: false, now);
        Assert.Equal(0, forgotten.RepeatCount);
        Assert.Equal(1, forgotten.IntervalDays);
        Assert.Equal(masteryBefore, forgotten.MasteryScore);
        Assert.Equal(WordLifecycleStage.Recognized, forgotten.LifecycleStage);

        // Remembered 未达成熟阈值：SM-2 推进排程，掌握度仍不变
        var remembered = new UserWordRelationship { UserId = Guid.NewGuid(), WordId = Guid.NewGuid() };
        sm2.ApplyReview(remembered, AssessmentResult.Remembered, now);
        WordLifecycleService.ApplyReview(remembered, WordQuizMode.Recognition, isCorrect: true, now);
        Assert.Equal(1, remembered.RepeatCount);
        Assert.Equal(WordLifecycleService.MasteryForStage(WordLifecycleStage.Recognized), remembered.MasteryScore);

        // 回忆阶段词在认识模式复习：阶段不前进也不回退（认识/回忆阶段不回退，SM-2 管遗忘）
        var recalled = new UserWordRelationship
        {
            UserId = Guid.NewGuid(),
            WordId = Guid.NewGuid(),
            LifecycleStage = WordLifecycleStage.Recalled,
            MasteryScore = WordLifecycleService.MasteryForStage(WordLifecycleStage.Recalled)
        };
        sm2.ApplyReview(recalled, AssessmentResult.Remembered, now);
        WordLifecycleService.ApplyReview(recalled, WordQuizMode.Recognition, isCorrect: true, now);
        Assert.Equal(WordLifecycleStage.Recalled, recalled.LifecycleStage);
        Assert.Equal(50, recalled.MasteryScore);
    }

    [Fact]
    public void Recall_pass_enters_candidate_pool_and_failure_stays()
    {
        var now = DateTimeOffset.UtcNow;
        var relationship = new UserWordRelationship
        {
            UserId = Guid.NewGuid(),
            WordId = Guid.NewGuid(),
            LifecycleStage = WordLifecycleStage.Recalled,
            MasteryScore = 50
        };

        // 回忆模式答错：留在回忆阶段
        WordLifecycleService.ApplyReview(relationship, WordQuizMode.Recall, isCorrect: false, now);
        Assert.Equal(WordLifecycleStage.Recalled, relationship.LifecycleStage);

        // 回忆模式答对（看义想词通过）→ 造句使用阶段，进入产出候选池
        WordLifecycleService.ApplyReview(relationship, WordQuizMode.Recall, isCorrect: true, now);
        Assert.Equal(WordLifecycleStage.PromptedUse, relationship.LifecycleStage);
        Assert.Equal(75, relationship.MasteryScore);
    }

    // ── 造句证据：确认与回退（真实 PG + 评分桩）─────────────────────

    [Fact]
    public async Task Prompted_sentence_confirms_correct_use_and_regresses_on_misuse()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, "lifecycle-sentence");

        var confirmedWord = await SeedWordWithStageAsync(db, user.Id, "confirmable", WordLifecycleStage.PromptedUse);
        var misusedWord = await SeedWordWithStageAsync(db, user.Id, "misusable", WordLifecycleStage.PromptedUse);
        var neutralWord = await SeedWordWithStageAsync(db, user.Id, "neutralword", WordLifecycleStage.PromptedUse);

        // 正确使用（A 档且句中含目标词）→ 确认待自发；指定目标词的产出不算自发（不毕业）
        var sentenceService = CreateSentenceService(db, Grade("A"));
        var log = await sentenceService.RateAsync(
            user.Id, confirmedWord.Id, confirmedWord.Lemma, $"This moment is {confirmedWord.Lemma}.", "life", "A2", CancellationToken.None);
        var confirmed = await GetRelationshipAsync(db, user.Id, confirmedWord.Id);
        Assert.Equal(WordLifecycleStage.PromptedUse, confirmed.LifecycleStage);
        Assert.NotNull(confirmed.PromptedUseConfirmedAt);
        Assert.Null(confirmed.GraduatedFreeExpressionLogId);
        Assert.Equal(75, confirmed.MasteryScore);

        // 使用错误（D 档但句中含目标词）→ 退回回忆阶段重进 SM-2 调度
        var regressService = CreateSentenceService(db, Grade("D", vocabulary: 1));
        await regressService.RateAsync(
            user.Id, misusedWord.Id, misusedWord.Lemma, $"I {misusedWord.Lemma} very much bad.", "life", "A2", CancellationToken.None);
        var regressed = await GetRelationshipAsync(db, user.Id, misusedWord.Id);
        Assert.Equal(WordLifecycleStage.Recalled, regressed.LifecycleStage);
        Assert.Null(regressed.PromptedUseConfirmedAt);
        Assert.Equal(0, regressed.RepeatCount);
        Assert.Equal(1, regressed.IntervalDays);
        Assert.Equal(50, regressed.MasteryScore);

        // 中性评分（C 档）：不确认也不回退
        var neutralService = CreateSentenceService(db, Grade("C"));
        await neutralService.RateAsync(
            user.Id, neutralWord.Id, neutralWord.Lemma, $"A {neutralWord.Lemma} sentence.", "life", "A2", CancellationToken.None);
        var neutral = await GetRelationshipAsync(db, user.Id, neutralWord.Id);
        Assert.Equal(WordLifecycleStage.PromptedUse, neutral.LifecycleStage);
        Assert.Null(neutral.PromptedUseConfirmedAt);
    }

    // ── 自发使用毕业（真实 PG + 评分桩）─────────────────────────────

    [Fact]
    public async Task Free_expression_graduates_spontaneous_use_with_trace()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, "lifecycle-free");

        var graduateWord = await SeedWordWithStageAsync(db, user.Id, "graduateme", WordLifecycleStage.PromptedUse);
        var absentWord = await SeedWordWithStageAsync(db, user.Id, "absentword", WordLifecycleStage.PromptedUse);
        var lowGradeWord = await SeedWordWithStageAsync(db, user.Id, "lowgradeword", WordLifecycleStage.PromptedUse);

        // 达标（A 档）自由表达中自发使用 → 毕业留痕（词 + FreeExpressionLog Id）
        var passing = new FreeExpressionService(db, new StubLlmFactory(new StaticRatingLlm(Grade("A"))), RatingOptions(), CreateScoreProfile(db));
        var log = await passing.RateAsync(user.Id, $"I want to {graduateWord.Lemma} every single day.", "A2", CancellationToken.None);
        var graduated = await GetRelationshipAsync(db, user.Id, graduateWord.Id);
        Assert.Equal(WordLifecycleStage.SpontaneousUse, graduated.LifecycleStage);
        Assert.Equal(log.Id, graduated.GraduatedFreeExpressionLogId);
        Assert.Equal(100, graduated.MasteryScore);
        Assert.NotNull(graduated.StageUpdatedAt);

        // 未出现的词不毕业
        var absent = await GetRelationshipAsync(db, user.Id, absentWord.Id);
        Assert.Equal(WordLifecycleStage.PromptedUse, absent.LifecycleStage);
        Assert.Null(absent.GraduatedFreeExpressionLogId);

        // 评分不达标（C 档）即使出现也不毕业
        var failing = new FreeExpressionService(db, new StubLlmFactory(new StaticRatingLlm(Grade("C"))), RatingOptions(), CreateScoreProfile(db));
        await failing.RateAsync(user.Id, $"The {lowGradeWord.Lemma} appears here.", "A2", CancellationToken.None);
        var lowGrade = await GetRelationshipAsync(db, user.Id, lowGradeWord.Id);
        Assert.Equal(WordLifecycleStage.PromptedUse, lowGrade.LifecycleStage);
        Assert.Null(lowGrade.GraduatedFreeExpressionLogId);
    }

    // ── Planner 候选池优先编排（真实 PG）────────────────────────────

    [Fact]
    public async Task Planner_prioritizes_candidate_pool_for_sentence_targets()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserWithBandAsync(db, "lifecycle-plan");
        await SeedWordPoolAsync(db, "pool");

        // 候选池词：prompted_use 未确认、带内（A2）
        var poolWord = await SeedWordWithStageAsync(db, user.Id, "poolword", WordLifecycleStage.PromptedUse, CefrLevel.A2);
        // 已确认词：不再重复编排
        var confirmedWord = await SeedWordWithStageAsync(db, user.Id, "confirmedpool", WordLifecycleStage.PromptedUse, CefrLevel.A2);
        var confirmedRel = await GetRelationshipAsync(db, user.Id, confirmedWord.Id);
        confirmedRel.PromptedUseConfirmedAt = DateTimeOffset.UtcNow;
        // 超带候选词：产出任务只用水平带内的词
        var overBandWord = await SeedWordWithStageAsync(db, user.Id, "overbandpool", WordLifecycleStage.PromptedUse, CefrLevel.C1);
        await db.SaveChangesAsync();

        var planService = CreatePlanService(db);
        var plan = await planService.GenerateAsync(user.Id, CancellationToken.None);
        var content = JsonSerializer.Deserialize<LearningPlanContent>(plan.ContentJson, JsonOptions)!;

        var allTargets = content.Days.SelectMany(day => day.SentenceTargets).ToList();
        Assert.Equal(poolWord.Lemma, content.Days[0].SentenceTargets[0]);
        Assert.DoesNotContain(confirmedWord.Lemma, allTargets);
        Assert.DoesNotContain(overBandWord.Lemma, allTargets);
    }

    // ── 每日词带阶段与考察模式（真实 PG）────────────────────────────

    [Fact]
    public async Task Daily_words_carry_stage_and_quiz_mode()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserWithBandAsync(db, "lifecycle-daily");

        // 回忆阶段的薄弱复习词（EstimatedKnownRate < 0.4 进复习位）
        var reviewWord = await SeedWordWithStageAsync(db, user.Id, "reviewword", WordLifecycleStage.Recalled, CefrLevel.A2);
        var reviewRel = await GetRelationshipAsync(db, user.Id, reviewWord.Id);
        reviewRel.EstimatedKnownRate = 0.1;
        // 带内新词（无关系记录 → 认识模式）
        var newWord = new Word
        {
            Lemma = $"brandnew{Guid.NewGuid():N}",
            Meanings = ["全新"],
            CefrLevel = CefrLevel.A2,
            DifficultyLevel = DifficultyLevel.Intermediate,
            ScenarioAnnotationVersion = ScenarioAnnotationWorker.CurrentVersion
        };
        db.Words.Add(newWord);
        await db.SaveChangesAsync();

        var planService = CreatePlanService(db);
        var daily = new DailyWordSelectionService(db, CreateScoreProfile(db), planService);
        var items = await daily.GetDailyAsync(user.Id, 10, CancellationToken.None);

        var reviewItem = items.FirstOrDefault(item => item.Id == reviewWord.Id);
        Assert.NotNull(reviewItem);
        Assert.Equal("recalled", reviewItem.Stage);
        Assert.Equal("recall", reviewItem.QuizMode);

        // 新词（无关系记录）默认认识阶段 + 看词知义（共享库词多，不带 Id 断言具体哪个新词）
        var newItems = items.Where(item => !item.IsWeak).ToList();
        Assert.NotEmpty(newItems);
        Assert.All(newItems, item =>
        {
            Assert.Equal("recognized", item.Stage);
            Assert.Equal("recognition", item.QuizMode);
        });
    }

    // ── 数据播种 ─────────────────────────────────────────────

    private static async Task<User> SeedUserAsync(ApplicationDbContext db, string name)
    {
        var user = new User { DisplayName = $"{name}-{Guid.NewGuid():N}" };
        db.Users.Add(user);
        db.UserProgress.Add(new UserProgress { UserId = user.Id, VocabularyScore = 50, CefrDisplay = "A2" });
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<User> SeedUserWithBandAsync(ApplicationDbContext db, string name) => await SeedUserAsync(db, name);

    private static async Task<Word> SeedWordWithStageAsync(
        ApplicationDbContext db, Guid userId, string lemmaPrefix, WordLifecycleStage stage, CefrLevel cefr = CefrLevel.A2)
    {
        // 字母后缀（词边界分词只保留字母，数字会切断 token 导致误判）
        var suffix = new string(Enumerable.Range(0, 8).Select(_ => (char)('a' + Random.Shared.Next(26))).ToArray());
        var word = new Word
        {
            Lemma = lemmaPrefix + suffix,
            Meanings = ["测试含义"],
            CefrLevel = cefr,
            DifficultyLevel = DifficultyLevel.Intermediate,
            ScenarioAnnotationVersion = ScenarioAnnotationWorker.CurrentVersion
        };
        db.Words.Add(word);
        db.UserWordRelationships.Add(new UserWordRelationship
        {
            UserId = userId,
            WordId = word.Id,
            LifecycleStage = stage,
            MasteryScore = WordLifecycleService.MasteryForStage(stage),
            StageUpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return word;
    }

    /// <summary>带内主攻场景词 + 超带接触词（与 LearningPlanTests 同口径，保证 Plan 正常生成）。</summary>
    private static async Task SeedWordPoolAsync(ApplicationDbContext db, string salt)
    {
        var suffix = $"{salt}-{Guid.NewGuid():N}";
        for (var i = 0; i < 12; i++)
        {
            var word = new Word
            {
                Lemma = $"inband{suffix}{i}",
                Meanings = ["含义"],
                CefrLevel = CefrLevel.A2,
                DifficultyLevel = DifficultyLevel.Intermediate,
                ScenarioAnnotationVersion = ScenarioAnnotationWorker.CurrentVersion
            };
            word.Scenarios.Add(new WordScenario { WordId = word.Id, ScenarioKey = "dining_out" });
            db.Words.Add(word);
        }

        await db.SaveChangesAsync();
    }

    /// <summary>跟踪加载（同一 DbContext 内服务与断言共享实例， setup 修改可直接保存）。</summary>
    private static async Task<UserWordRelationship> GetRelationshipAsync(ApplicationDbContext db, Guid userId, Guid wordId)
    {
        return await db.UserWordRelationships
            .FirstAsync(item => item.UserId == userId && item.WordId == wordId);
    }

    // ── 服务装配 ─────────────────────────────────────────────

    private static SentenceService CreateSentenceService(ApplicationDbContext db, SentenceRatingResponse rating)
    {
        var scoreProfile = CreateScoreProfile(db);
        return new SentenceService(
            db,
            new StubLlmFactory(new StaticRatingLlm(rating)),
            RatingOptions(),
            new LearningPlanService(db, scoreProfile),
            scoreProfile);
    }

    private static LearningPlanService CreatePlanService(ApplicationDbContext db)
        => new(db, CreateScoreProfile(db));

    private static ScoreProfileService CreateScoreProfile(ApplicationDbContext db)
        => new(db, new ScoreMappingService(new ScoreMappingOptions()));

    private static IOptions<LlmSentenceRatingOptions> RatingOptions() => Options.Create(new LlmSentenceRatingOptions());

    private static SentenceRatingResponse Grade(string grade, int vocabulary = 4) =>
        new(vocabulary, vocabulary, vocabulary, vocabulary, grade, string.Empty, [], DifficultyLevel.Intermediate, string.Empty);

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
