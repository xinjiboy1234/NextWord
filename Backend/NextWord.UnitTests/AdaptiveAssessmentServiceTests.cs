using System.Text.Json;
using Microsoft.EntityFrameworkCore;
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
/// T-004 自适应分块测评：真实 PG + 可控 LLM 评分桩，验证
/// 产出题占比、词池纪律、自适应升降带、2–3 块收敛、表达力综合分主定级。
/// </summary>
public class AdaptiveAssessmentServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Strong_user_rises_bands_and_levels_by_expression_only()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var service = CreateService(db, new StubLlmProvider(5, 5, 5, 5));
        var user = await SeedPoolAsync(db);
        var assessment = await service.StartInitialAsync(user.Id, CancellationToken.None);

        // 块 1：从未测评用户从 A2 起步；产出题 3/5 = 60%
        var response = await service.GetNextBlockAsync(assessment.Id, CancellationToken.None);
        Assert.False(response.Converged);
        var block = response.Block!;
        Assert.Equal(1, block.BlockIndex);
        Assert.Equal(CefrLevel.A2, block.Band);
        Assert.Equal(3, block.Production.Count);
        Assert.Equal(2, block.Production.Count(item => item.Kind == "sentence"));
        Assert.Single(block.Production, item => item.Kind == "scenario");
        Assert.Single(block.Vocabulary);
        Assert.NotNull(block.Reading);

        // 词池纪律：出题词全部在本带内，且不含 utility=low 的 a2lowword
        Assert.All(block.Production.Where(item => item.Kind == "sentence"),
            item => Assert.StartsWith("a2word", item.TargetWord));
        Assert.StartsWith("a2word", block.Vocabulary[0].Word);

        // GET 幂等：重取同一块，题目不变
        var again = await service.GetNextBlockAsync(assessment.Id, CancellationToken.None);
        Assert.Equal(block.Production.Select(item => item.Id), again.Block!.Production.Select(item => item.Id));
        Assert.Equal(block.Vocabulary[0].Word, again.Block.Vocabulary[0].Word);

        // 提交：产出全对（LLM 满分），识别题也答对（与表达同档，不触发 T-042 防伪闸）→ 升带但不收敛（1 块）
        var result1 = await service.SubmitBlockAsync(assessment.Id, 1,
            await AnswersAsync(service, assessment.Id, 1, recognitionCorrect: true), CancellationToken.None);
        Assert.False(result1.Converged);
        Assert.Equal(100, result1.BlockExpressionScore);
        Assert.Equal(CefrLevel.B1, result1.NextBand);

        // 块 2：升带到 B1，仍满分 → 升带，2 块未稳定不收敛
        var block2 = (await service.GetNextBlockAsync(assessment.Id, CancellationToken.None)).Block!;
        Assert.Equal(2, block2.BlockIndex);
        Assert.Equal(CefrLevel.B1, block2.Band);
        Assert.All(block2.Production.Where(item => item.Kind == "sentence"),
            item => Assert.StartsWith("b1word", item.TargetWord!));
        var result2 = await service.SubmitBlockAsync(assessment.Id, 2,
            await AnswersAsync(service, assessment.Id, 2, recognitionCorrect: true), CancellationToken.None);
        Assert.False(result2.Converged);
        Assert.Equal(CefrLevel.B2, result2.NextBand);

        // 块 3：B2，满分 → 满 3 块收敛，总量 15 题
        var block3 = (await service.GetNextBlockAsync(assessment.Id, CancellationToken.None)).Block!;
        Assert.Equal(CefrLevel.B2, block3.Band);
        var result3 = await service.SubmitBlockAsync(assessment.Id, 3,
            await AnswersAsync(service, assessment.Id, 3, recognitionCorrect: true), CancellationToken.None);
        Assert.True(result3.Converged);
        Assert.Null(result3.NextBand);

        // 主定级 = 表达力综合分（100 → C1）；识别同档（全对）只作参考，防伪闸不矫正
        var final = result3.Final!;
        Assert.Equal(CefrLevel.C1, final.OverallLevel);
        Assert.Equal(100, final.ExpressionScore);
        Assert.Equal(100, final.VocabularyReferenceScore);
        Assert.Equal(100, final.ReadingReferenceScore);
        Assert.Null(final.OriginalLevelBeforeGuard);
        Assert.Equal(5.0, final.Dimensions.Grammar);
        Assert.NotEmpty(final.Dimensions.Comments);

        var done = await service.GetAsync(assessment.Id, CancellationToken.None);
        Assert.Equal(AssessmentStatus.Completed, done!.Status);
        Assert.Equal(CefrLevel.C1, done.FinalLevel);

        // 产出题全部走 LLM 评分链路留痕：3 块 × 3 题 = 9 条 SentenceLog
        Assert.Equal(9, await db.SentenceLogs.CountAsync(item => item.UserId == user.Id));

        // T-054：逐题 AI 评语/改写随块评分持久化（stub：suggestion 固定文案，aiRevision 回显作答）
        var blockRecord = await db.AssessmentRecords
            .SingleAsync(item => item.AssessmentId == assessment.Id && item.QuestionType == "block:1");
        using var scoresDoc = JsonDocument.Parse(blockRecord.ScoresJson);
        Assert.All(scoresDoc.RootElement.GetProperty("production").EnumerateArray(), item =>
        {
            Assert.Equal("stub suggestion", item.GetProperty("suggestion").GetString());
            Assert.Equal("I wrote a long enough answer here.", item.GetProperty("aiRevision").GetString());
        });

        var progress = await db.UserProgress.SingleAsync(item => item.UserId == user.Id);
        Assert.True(progress.HasCompletedInitialAssessment);
        Assert.Equal(CefrLevel.C1, progress.OverallLevel);

        // 收敛后再取块：直接回最终结果
        var after = await service.GetNextBlockAsync(assessment.Id, CancellationToken.None);
        Assert.True(after.Converged);
        Assert.Equal(CefrLevel.C1, after.Final!.OverallLevel);
    }

    [Fact]
    public async Task Weak_user_drops_band_and_levels_low()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var service = CreateService(db, new StubLlmProvider(1, 1, 0, 0)); // 12 分 → 降带
        var user = await SeedPoolAsync(db);
        var assessment = await service.StartInitialAsync(user.Id, CancellationToken.None);

        var block1 = (await service.GetNextBlockAsync(assessment.Id, CancellationToken.None)).Block!;
        Assert.Equal(CefrLevel.A2, block1.Band);
        var result1 = await service.SubmitBlockAsync(assessment.Id, 1,
            await AnswersAsync(service, assessment.Id, 1, recognitionCorrect: true), CancellationToken.None);
        Assert.False(result1.Converged);
        Assert.Equal(CefrLevel.A1, result1.NextBand); // 表现差降带

        var block2 = (await service.GetNextBlockAsync(assessment.Id, CancellationToken.None)).Block!;
        Assert.Equal(CefrLevel.A1, block2.Band);
        await service.SubmitBlockAsync(assessment.Id, 2,
            await AnswersAsync(service, assessment.Id, 2, recognitionCorrect: true), CancellationToken.None);

        // A1 到底后仍在低位，第 3 块收敛
        var block3 = (await service.GetNextBlockAsync(assessment.Id, CancellationToken.None)).Block!;
        Assert.Equal(CefrLevel.A1, block3.Band);
        var result3 = await service.SubmitBlockAsync(assessment.Id, 3,
            await AnswersAsync(service, assessment.Id, 3, recognitionCorrect: true), CancellationToken.None);
        Assert.True(result3.Converged);
        Assert.Equal(CefrLevel.A1, result3.Final!.OverallLevel);
        Assert.Equal(12, result3.Final.ExpressionScore);
    }

    [Fact]
    public async Task Stable_user_converges_after_two_blocks()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var service = CreateService(db, new StubLlmProvider(3, 3, 2, 2)); // 52 分 → 保持
        var user = await SeedPoolAsync(db);
        var assessment = await service.StartInitialAsync(user.Id, CancellationToken.None);

        await service.GetNextBlockAsync(assessment.Id, CancellationToken.None);
        var result1 = await service.SubmitBlockAsync(assessment.Id, 1,
            await AnswersAsync(service, assessment.Id, 1, recognitionCorrect: true), CancellationToken.None);
        Assert.False(result1.Converged);
        Assert.Equal(CefrLevel.A2, result1.NextBand); // 稳定不升不降

        await service.GetNextBlockAsync(assessment.Id, CancellationToken.None);
        var result2 = await service.SubmitBlockAsync(assessment.Id, 2,
            await AnswersAsync(service, assessment.Id, 2, recognitionCorrect: true), CancellationToken.None);
        Assert.True(result2.Converged); // 2 块稳定即收敛，总量 10 题
        Assert.Equal(52, result2.Final!.ExpressionScore);
        Assert.Equal(CefrLevel.B1, result2.Final.OverallLevel); // T-023 新分带（B2 起点 70）：52 → B1
        Assert.Null(result2.Final.OriginalLevelBeforeGuard); // 识别全对（参考 C1）反向不矫正
    }

    /// <summary>T-042 验收 §4-1/5：表达 76（定级 B2）+ 词汇识别全错（参考 A1），档差 ≥2 → 下调 1 档至 B1 并留痕。</summary>
    [Fact]
    public async Task Inflated_expression_is_adjusted_by_recognition_guard()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var service = CreateService(db, new StubLlmProvider(4, 4, 4, 3)); // 76 分 ≥70 → 每块升带
        var user = await SeedPoolAsync(db);
        var assessment = await service.StartInitialAsync(user.Id, CancellationToken.None);

        await service.GetNextBlockAsync(assessment.Id, CancellationToken.None);
        var result1 = await service.SubmitBlockAsync(assessment.Id, 1,
            await AnswersAsync(service, assessment.Id, 1, recognitionCorrect: false), CancellationToken.None);
        Assert.Equal(76, result1.BlockExpressionScore);
        Assert.Equal(CefrLevel.B1, result1.NextBand);

        await service.GetNextBlockAsync(assessment.Id, CancellationToken.None);
        await service.SubmitBlockAsync(assessment.Id, 2,
            await AnswersAsync(service, assessment.Id, 2, recognitionCorrect: false), CancellationToken.None);
        await service.GetNextBlockAsync(assessment.Id, CancellationToken.None);
        var result3 = await service.SubmitBlockAsync(assessment.Id, 3,
            await AnswersAsync(service, assessment.Id, 3, recognitionCorrect: false), CancellationToken.None);
        Assert.True(result3.Converged);

        // 表达定级 B2 − 识别 A1 = 3 档 → 下调 1 档至 B1；结果含用户可读矫正说明
        var final = result3.Final!;
        Assert.Equal(76, final.ExpressionScore);
        Assert.Equal(CefrLevel.B1, final.OverallLevel);
        Assert.Equal(CefrLevel.B2, final.OriginalLevelBeforeGuard);
        Assert.Contains(final.Dimensions.Comments,
            comment => comment.Contains("表达表现 B2") && comment.Contains("调整为 B1"));

        // 留痕可查：Assessment.FinalLevel 为矫正后定级，FinalLevel 记录含原定级
        var done = await service.GetAsync(assessment.Id, CancellationToken.None);
        Assert.Equal(CefrLevel.B1, done!.FinalLevel);
        var finalRecord = done.Records.Single(item => item.Step == AssessmentStepType.FinalLevel);
        var recorded = JsonSerializer.Deserialize<AssessmentFinalResult>(finalRecord.ScoresJson, JsonOptions)!;
        Assert.Equal(CefrLevel.B1, recorded.OverallLevel);
        Assert.Equal(CefrLevel.B2, recorded.OriginalLevelBeforeGuard);

        // T-054：历史列表投影——表达综合分与识别矫正标记来自 FinalLevel 记录
        var listItem = Assert.Single(await service.ListForUserAsync(user.Id, CancellationToken.None));
        Assert.Equal(assessment.Id, listItem.Id);
        Assert.Equal(AssessmentStatus.Completed, listItem.Status);
        Assert.Equal(CefrLevel.B1, listItem.FinalLevel);
        Assert.Equal(76, listItem.ExpressionScore);
        Assert.True(listItem.GuardAdjusted);

        // 矫正传导（qa-t042 P1）：三维分数先验 clamp 到矫正后档内（B1 上限 69），CefrDisplay 取矫正后档
        var progress = await db.UserProgress.SingleAsync(item => item.UserId == user.Id);
        Assert.Equal(CefrLevel.B1, progress.OverallLevel);
        Assert.Equal(69, progress.VocabularyScore);
        Assert.Equal(69, progress.ReadingScore);
        Assert.Equal(69, progress.WritingScore);
        var scores = await new ScoreProfileService(db, new ScoreMappingService(new ScoreMappingOptions()))
            .GetScoresAsync(user.Id, CancellationToken.None);
        Assert.Equal("B1", scores.CefrDisplay);

        // Planner 词池带与矫正后定级一致：背词队列与造句目标全部带内（B1，不再给 B2 习语）
        var planService = new LearningPlanService(db, new ScoreProfileService(db, new ScoreMappingService(new ScoreMappingOptions())));
        var plan = await planService.GenerateAsync(user.Id, CancellationToken.None);
        var content = JsonSerializer.Deserialize<LearningPlanContent>(plan.ContentJson, JsonOptions)!;
        var plannedIds = content.Days.SelectMany(day => day.WordIds).ToList();
        var plannedLevels = await db.Words
            .Where(word => plannedIds.Contains(word.Id))
            .Select(word => word.CefrLevel)
            .ToListAsync();
        Assert.NotEmpty(plannedLevels);
        Assert.All(plannedLevels, level => Assert.Equal(CefrLevel.B1, level));
        var targets = content.Days.SelectMany(day => day.SentenceTargets).ToList();
        Assert.NotEmpty(targets);
        var targetWords = await db.Words.Where(word => targets.Contains(word.Lemma)).ToListAsync();
        Assert.Equal(targets.Distinct().Count(), targetWords.Count);
        Assert.All(targetWords, word => Assert.Equal(CefrLevel.B1, word.CefrLevel));
    }

    /// <summary>T-042 验收 §4-4：识别样本缺失（用户全跳过识别题）时不矫正不报错。</summary>
    [Fact]
    public async Task Missing_recognition_sample_disables_guard()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var service = CreateService(db, new StubLlmProvider(4, 4, 4, 3)); // 76 分
        var user = await SeedPoolAsync(db);
        var assessment = await service.StartInitialAsync(user.Id, CancellationToken.None);

        await service.GetNextBlockAsync(assessment.Id, CancellationToken.None);
        await service.SubmitBlockAsync(assessment.Id, 1,
            await AnswersAsync(service, assessment.Id, 1, skipRecognition: true), CancellationToken.None);
        await service.GetNextBlockAsync(assessment.Id, CancellationToken.None);
        await service.SubmitBlockAsync(assessment.Id, 2,
            await AnswersAsync(service, assessment.Id, 2, skipRecognition: true), CancellationToken.None);
        await service.GetNextBlockAsync(assessment.Id, CancellationToken.None);
        var result3 = await service.SubmitBlockAsync(assessment.Id, 3,
            await AnswersAsync(service, assessment.Id, 3, skipRecognition: true), CancellationToken.None);
        Assert.True(result3.Converged);

        // 识别样本缺失 → 防伪闸不触发，表达定级 B2 原样保留
        Assert.Equal(CefrLevel.B2, result3.Final!.OverallLevel);
        Assert.Null(result3.Final.OriginalLevelBeforeGuard);
    }

    [Fact]
    public async Task Reading_correct_index_is_not_constant()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var service = CreateService(db, new StubLlmProvider(3, 3, 3, 3));
        await SeedPoolAsync(db);

        // 多次生成块，阅读题正确答案位置不得恒为 0（旧缺陷：恒 index 0）
        var indices = new List<int>();
        for (var i = 0; i < 12; i++)
        {
            var user = new User { DisplayName = $"reading-random-{i}" };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            var assessment = await service.StartInitialAsync(user.Id, CancellationToken.None);
            await service.GetNextBlockAsync(assessment.Id, CancellationToken.None);
            var stored = await service.GetAsync(assessment.Id, CancellationToken.None);
            var record = stored!.Records.Single(item => item.Step == AssessmentStepType.AdaptiveBlock);
            using var doc = JsonDocument.Parse(record.QuestionsJson);
            indices.Add(doc.RootElement.GetProperty("reading").GetProperty("correctIndex").GetInt32());
        }

        Assert.Contains(indices, index => index != 0);
    }

    /// <summary>T-054：历史列表只含本人测评、按开始时间倒序；无 FinalLevel 记录时表达分降级为 null。</summary>
    [Fact]
    public async Task List_history_returns_only_own_assessments_descending()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var service = CreateService(db, new StubLlmProvider(3, 3, 3, 3));
        var user = new User { DisplayName = "assessment-list" };
        var other = new User { DisplayName = "assessment-list-other" };
        db.Users.AddRange(user, other);
        await db.SaveChangesAsync();

        var first = await service.StartInitialAsync(user.Id, CancellationToken.None);
        await service.SkipInitialAsync(user.Id, CancellationToken.None);
        var second = await service.StartInitialAsync(user.Id, CancellationToken.None);
        var otherAssessment = await service.StartInitialAsync(other.Id, CancellationToken.None);

        var list = await service.ListForUserAsync(user.Id, CancellationToken.None);
        Assert.Equal(2, list.Count);
        Assert.Equal(second.Id, list[0].Id);
        Assert.Equal(first.Id, list[1].Id);
        Assert.DoesNotContain(list, item => item.Id == otherAssessment.Id);
        Assert.True(list[0].StartAt >= list[1].StartAt);

        // 跳过完成的测评无 FinalLevel 记录：表达综合分 null、未矫正
        Assert.Equal(AssessmentStatus.Completed, list[1].Status);
        Assert.Equal(CefrLevel.A2, list[1].FinalLevel);
        Assert.Null(list[1].ExpressionScore);
        Assert.False(list[1].GuardAdjusted);
        // 进行中的测评同样降级
        Assert.Equal(AssessmentStatus.InProgress, list[0].Status);
        Assert.Null(list[0].ExpressionScore);
    }

    // ── 工具 ────────────────────────────────────────────────

    private static AssessmentService CreateService(ApplicationDbContext db, ILLMProvider llm)
    {
        var users = new UserRepository(db);
        var scoreProfile = new ScoreProfileService(db, new ScoreMappingService(new ScoreMappingOptions()));
        var sentences = new SentenceService(db, new StubLlmFactory(llm), Options.Create(new LlmSentenceRatingOptions()), new LearningPlanService(db, scoreProfile), scoreProfile);
        return new AssessmentService(db, new AssessmentScoringService(new ScoreMappingOptions()), sentences, users, scoreProfile, new StubEvaluationReports());
    }

    private static async Task<User> SeedPoolAsync(ApplicationDbContext db)
    {
        // 共享测试库中 Lemma 有唯一索引：每次播种带随机后缀，断言用 StartsWith 前缀匹配
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = new User { DisplayName = "assessment-test" };
        db.Users.Add(user);

        foreach (var band in new[] { CefrLevel.A1, CefrLevel.A2, CefrLevel.B1, CefrLevel.B2, CefrLevel.C1 })
        {
            var prefix = band.ToString().ToLowerInvariant();
            for (var i = 0; i < 8; i++)
            {
                var word = new Word
                {
                    Lemma = $"{prefix}word{i}{suffix}",
                    Meanings = [$"{prefix} 含义{i}"],
                    CefrLevel = band,
                    DifficultyLevel = band <= CefrLevel.A2 ? DifficultyLevel.Basic : band <= CefrLevel.B2 ? DifficultyLevel.Intermediate : DifficultyLevel.Advanced,
                    Utility = WordUtility.High,
                    Role = ExpressionRole.CoreVerb,
                    // 标记为已标注，避免共享库中 ScenarioAnnotationWorker 测试把本组词扫进待标队列
                    ScenarioAnnotationVersion = ScenarioAnnotationWorker.CurrentVersion
                };
                word.Scenarios.Add(new WordScenario { WordId = word.Id, ScenarioKey = "dining_out" });
                db.Words.Add(word);
            }
        }

        // 干扰项：本带 low utility 词不得入池；无 utility 标注词不得入池
        db.Words.Add(new Word { Lemma = $"a2lowword{suffix}", Meanings = ["低价值词"], CefrLevel = CefrLevel.A2, Utility = WordUtility.Low, ScenarioAnnotationVersion = ScenarioAnnotationWorker.CurrentVersion });
        db.Words.Add(new Word { Lemma = $"a2unmarked{suffix}", Meanings = ["未标注词"], CefrLevel = CefrLevel.A2, Utility = null, ScenarioAnnotationVersion = ScenarioAnnotationWorker.CurrentVersion });

        foreach (var band in new[] { CefrLevel.A1, CefrLevel.A2, CefrLevel.B1, CefrLevel.B2 })
        {
            var prefix = band.ToString().ToLowerInvariant();
            db.Articles.Add(new Article
            {
                Title = $"{band} article",
                Content = $"This story mentions {prefix}word0{suffix} and {prefix}word1{suffix} many times.",
                CefrLevel = band,
                WordCount = 40
            });
        }

        await db.SaveChangesAsync();
        return user;
    }

    /// <summary>构造作答：产出题给非空文本；识别题按题库中正确答案选择全对或全错；skipRecognition 时识别题整题跳过（无作答记录）。</summary>
    private static async Task<List<AssessmentAnswerItem>> AnswersAsync(
        AssessmentService service, Guid assessmentId, int blockIndex, bool recognitionCorrect = false, bool skipRecognition = false)
    {
        var stored = await service.GetAsync(assessmentId, CancellationToken.None);
        var record = stored!.Records.Single(item => item.QuestionType == $"block:{blockIndex}");
        using var doc = JsonDocument.Parse(record.QuestionsJson);
        var root = doc.RootElement;

        var answers = new List<AssessmentAnswerItem>();
        foreach (var item in root.GetProperty("production").EnumerateArray())
        {
            answers.Add(new AssessmentAnswerItem(item.GetProperty("id").GetString()!, "I wrote a long enough answer here.", null, null));
        }

        if (skipRecognition)
        {
            return answers;
        }

        foreach (var item in root.GetProperty("vocabulary").EnumerateArray())
        {
            var correct = item.GetProperty("correctIndex").GetInt32();
            var optionCount = item.GetProperty("options").GetArrayLength();
            answers.Add(new AssessmentAnswerItem(
                item.GetProperty("id").GetString()!, null,
                recognitionCorrect ? correct : (correct + 1) % optionCount, null));
        }

        if (root.TryGetProperty("reading", out var reading) && reading.ValueKind == JsonValueKind.Object)
        {
            var correct = reading.GetProperty("correctIndex").GetInt32();
            var optionCount = reading.GetProperty("options").GetArrayLength();
            answers.Add(new AssessmentAnswerItem(
                reading.GetProperty("id").GetString()!, null,
                recognitionCorrect ? correct : (correct + 1) % optionCount, 0));
        }

        return answers;
    }

    private sealed class StubLlmProvider(int grammar, int natural, int vocabulary, int relevance) : ILLMProvider
    {
        public Task<SentenceRatingResponse> RateSentenceAsync(SentenceRatingRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new SentenceRatingResponse(
                grammar, natural, vocabulary, relevance, "B", request.UserSentence,
                [$"stub-tag-{grammar}"], DifficultyLevel.Basic, "stub suggestion"));

        public Task<DifficultyRating> RateDifficultyAsync(ItemRatingRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<DefinitionResponse> GetDefinitionAsync(DefinitionRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<VocabExtractResponse> ExtractVocabAsync(VocabExtractRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<CommentReplyResponse> ReplyToCommentAsync(CommentReplyRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ScenarioAnnotationResponse> AnnotateScenarioAsync(ScenarioAnnotationRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<WeaknessProfileResponse> GenerateWeaknessProfileAsync(WeaknessProfileRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<BottleneckInsightResponse> GenerateBottleneckInsightAsync(BottleneckInsightRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    private sealed class StubLlmFactory(ILLMProvider provider) : IUserLlmProviderFactory
    {
        public Task<ILLMProvider> GetForUserAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(provider);
    }

    private sealed class StubEvaluationReports : IEvaluationReportService
    {
        public Task<long> EnqueueForUserAsync(Guid userId, string triggerType, Guid? assessmentId, CancellationToken cancellationToken) => Task.FromResult(1L);
        public Task ProcessJobAsync(BackgroundJob job, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
