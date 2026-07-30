using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Domain.Scenarios;
using NextWord.Infrastructure.Data;
using NextWord.Infrastructure.Services;

namespace NextWord.UnitTests;

/// <summary>
/// T-032 画像冷启动「探索周」（真实 PG，DESIGN-cold-start-profile §4）：
/// 触发器两条件（满 7 天 / 产出证据 ≥10 条）与「每用户仅一次」（标记位与瓶颈重生成不混淆）；
/// 探索周进度口径（第 x/7 天、N = max(0, 10 − 证据条数)、场景轮转出题）；
/// Verifier 冷启动放宽档（条数不足降 low 标 Verified 注「初步判断」，伪造/数值核查不放宽，默认档恢复纪律）。
/// </summary>
public class ColdStartExplorationTests
{
    // ── 触发器条件 ─────────────────────────────────────────

    [Fact]
    public async Task Trigger_fires_after_seven_days_without_evidence()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, "cold-7d", registeredDaysAgo: 8);
        var service = new ColdStartExplorationService(db);

        var evaluation = await service.EvaluateTriggerAsync(user.Id, CancellationToken.None);

        Assert.True(evaluation.ShouldTrigger);
        Assert.Equal(8, evaluation.DaysSinceRegistration);
        Assert.Equal(0, evaluation.EvidenceCount);
    }

    [Fact]
    public async Task Trigger_fires_when_evidence_reaches_ten_within_week()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, "cold-10e", registeredDaysAgo: 1);
        await SeedSentenceLogsAsync(db, user.Id, 6);
        await SeedFreeExpressionsAsync(db, user.Id, 4);
        var service = new ColdStartExplorationService(db);

        var evaluation = await service.EvaluateTriggerAsync(user.Id, CancellationToken.None);

        Assert.True(evaluation.ShouldTrigger);
        Assert.Equal(10, evaluation.EvidenceCount);
    }

    [Fact]
    public async Task Trigger_stays_quiet_within_week_below_evidence_target()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, "cold-quiet", registeredDaysAgo: 2);
        await SeedSentenceLogsAsync(db, user.Id, 3);
        var service = new ColdStartExplorationService(db);

        var evaluation = await service.EvaluateTriggerAsync(user.Id, CancellationToken.None);

        Assert.False(evaluation.ShouldTrigger);
        Assert.Equal(3, evaluation.EvidenceCount);
    }

    [Fact]
    public async Task Trigger_fires_only_once_and_distinguishes_bottleneck_regens()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var service = new ColdStartExplorationService(db);

        // 瓶颈触发（T-007）的画像重生成（ModelProfileId = "weakness-profile"）不算冷启动重生成，不消耗「仅一次」
        var bottleneckUser = await SeedUserAsync(db, "cold-bottleneck", registeredDaysAgo: 8);
        db.WeaknessProfiles.Add(new WeaknessProfile { UserId = bottleneckUser.Id, ModelProfileId = "weakness-profile" });
        await db.SaveChangesAsync();
        var bottleneckEval = await service.EvaluateTriggerAsync(bottleneckUser.Id, CancellationToken.None);
        Assert.True(bottleneckEval.ShouldTrigger);

        // 已做过冷启动重生成（标记位画像存在）→ 条件再满足也不重复触发
        var doneUser = await SeedUserAsync(db, "cold-done", registeredDaysAgo: 8);
        db.WeaknessProfiles.Add(new WeaknessProfile
        {
            UserId = doneUser.Id,
            ModelProfileId = ColdStartExplorationService.ColdStartModelProfileId
        });
        await db.SaveChangesAsync();
        var doneEval = await service.EvaluateTriggerAsync(doneUser.Id, CancellationToken.None);
        Assert.False(doneEval.ShouldTrigger);
    }

    // ── 探索周进度口径 ─────────────────────────────────────

    [Fact]
    public async Task Exploration_week_reports_day_remaining_and_rotating_scenario_task()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, "cold-week", registeredDaysAgo: 2);
        await SeedSentenceLogsAsync(db, user.Id, 3);
        await SeedFreeExpressionsAsync(db, user.Id, 1);
        await SeedAnnotatedWordAsync(db, "cold-week");
        var service = new ColdStartExplorationService(db);

        var status = await service.GetExplorationWeekAsync(user.Id, CancellationToken.None);

        Assert.True(status.Active);
        Assert.Equal(3, status.Day); // 注册第 3 天（1 起）
        Assert.Equal(7, status.TotalDays);
        Assert.Equal(4, status.EvidenceCount);
        Assert.Equal(6, status.RemainingEvidence); // N = max(0, 10 − 4)
        Assert.True(ScenarioTaxonomy.IsSubScenarioKey(status.ScenarioKey));
        Assert.False(string.IsNullOrWhiteSpace(status.ScenarioName));
        Assert.Contains(status.ScenarioName, status.Prompt);
    }

    [Fact]
    public async Task Exploration_week_ends_after_seven_days()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, "cold-expired", registeredDaysAgo: 7);
        var service = new ColdStartExplorationService(db);

        var status = await service.GetExplorationWeekAsync(user.Id, CancellationToken.None);

        Assert.False(status.Active);
    }

    [Fact]
    public async Task Exploration_remaining_floors_at_zero_when_evidence_exceeds_target()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, "cold-over", registeredDaysAgo: 0);
        await SeedSentenceLogsAsync(db, user.Id, 12);
        var service = new ColdStartExplorationService(db);

        var status = await service.GetExplorationWeekAsync(user.Id, CancellationToken.None);

        Assert.True(status.Active);
        Assert.Equal(12, status.EvidenceCount);
        Assert.Equal(0, status.RemainingEvidence);
    }

    // ── Verifier 冷启动放宽档 ──────────────────────────────

    [Fact]
    public async Task Verifier_relaxed_downgrades_thin_evidence_but_keeps_mechanical_checks()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, "cold-verifier", registeredDaysAgo: 1);
        var logs = await SeedSentenceLogsAsync(db, user.Id, 1);
        var verifier = new FindingVerifier(db);

        var thin = new ProfileFindingDraft(
            FindingDimension.Skill, "grammar", FindingPolarity.Weakness, "语法稳定性样本尚少。",
            [new EvidenceClaim("sentence_log", logs[0].Id.ToString(), "grammar", "<=", 4)],
            FindingConfidence.Medium);
        var forged = thin with
        {
            Statement = "伪造引用。",
            Evidence = [new EvidenceClaim("sentence_log", Guid.NewGuid().ToString(), "grammar", "<=", 4)]
        };
        var tampered = thin with
        {
            Statement = "篡改数值。",
            Evidence = [new EvidenceClaim("sentence_log", logs[0].Id.ToString(), "grammar", "<=", 1)]
        };

        var relaxed = await verifier.VerifyAsync(user.Id, null, [thin, forged, tampered], CancellationToken.None, relaxedColdStart: true);

        // 条数不足（medium 需 ≥2 实际 1）→ 降 low 标 Verified 注「初步判断」
        Assert.Equal(FindingVerification.Verified, relaxed[0].Verification);
        Assert.Equal(FindingConfidence.Low, relaxed[0].Draft.Confidence);
        Assert.Contains("初步判断", relaxed[0].Note);
        // 伪造/数值不符的机械核查不放宽
        Assert.Equal(FindingVerification.Questioned, relaxed[1].Verification);
        Assert.Equal(FindingVerification.Questioned, relaxed[2].Verification);
        Assert.Contains("不属实", relaxed[2].Note);

        // 默认档（第二份画像起）恢复既有纪律：同一条不足草稿仍判存疑
        var strict = await verifier.VerifyAsync(user.Id, null, [thin], CancellationToken.None);
        Assert.Equal(FindingVerification.Questioned, strict[0].Verification);
        Assert.Contains("样本量不足", strict[0].Note);
    }

    [Fact]
    public async Task Cold_start_profile_persists_low_verified_with_marker_and_closes_trigger()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, "cold-chain", registeredDaysAgo: 8);
        var logs = await SeedSentenceLogsAsync(db, user.Id, 1);

        var llm = new StubProfilerLlm(() =>
        [
            new ProfileFindingDraft(
                FindingDimension.Skill, "grammar", FindingPolarity.Weakness, "语法样本不足，先记初步判断。",
                [new EvidenceClaim("sentence_log", logs[0].Id.ToString(), "grammar", "<=", 4)],
                FindingConfidence.Medium)
        ]);
        var profileService = new WeaknessProfileService(db, new WeaknessProfiler(db, new StubLlmFactory(llm)), new FindingVerifier(db));

        var profile = await profileService.GenerateAsync(user.Id, null, CancellationToken.None, coldStart: true);

        Assert.Equal(ColdStartExplorationService.ColdStartModelProfileId, profile.ModelProfileId);
        var finding = Assert.Single(profile.Findings);
        Assert.Equal(FindingVerification.Verified, finding.Verification);
        Assert.Equal(FindingConfidence.Low, finding.Confidence);
        Assert.Contains("初步判断", finding.VerificationNote);

        // 闭环：冷启动重生成已落标记位 → 触发器不再放行（每用户仅一次）
        var trigger = new ColdStartExplorationService(db);
        var evaluation = await trigger.EvaluateTriggerAsync(user.Id, CancellationToken.None);
        Assert.False(evaluation.ShouldTrigger);
    }

    // ── T-032 验收阻断修复：自由表达证据进画像 ─────────────

    [Fact]
    public async Task Profiler_aggregates_free_expression_logs_as_evidence_input()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, "cold-profiler-free", registeredDaysAgo: 1);
        await SeedFreeExpressionsAsync(db, user.Id, 3);
        var seeded = await db.FreeExpressionLogs.AsNoTracking()
            .Where(log => log.UserId == user.Id)
            .ToListAsync();

        var llm = new CapturingProfilerLlm();
        var profiler = new WeaknessProfiler(db, new StubLlmFactory(llm));

        await profiler.BuildDraftsAsync(user.Id, null, CancellationToken.None);

        Assert.NotNull(llm.LastRequest);
        var request = llm.LastRequest;
        // 自由表达留痕与造句留痕同权进入 Profiler 输入（含可引用的记录 Id 与数值）
        Assert.Equal(3, request.FreeExpressionLogs.Count);
        Assert.All(request.FreeExpressionLogs, evidence =>
            Assert.Contains(seeded, log => log.Id == evidence.Id && log.AiScore == evidence.AiScore));
    }

    [Fact]
    public async Task Verifier_checks_free_expression_log_evidence_with_same_discipline()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, "cold-verifier-free", registeredDaysAgo: 1);
        await SeedFreeExpressionsAsync(db, user.Id, 1);
        var freeLog = await db.FreeExpressionLogs.AsNoTracking()
            .SingleAsync(log => log.UserId == user.Id); // AiScore = 80（播种口径）
        var verifier = new FindingVerifier(db);

        var truthful = new ProfileFindingDraft(
            FindingDimension.Skill, "vocabulary", FindingPolarity.Weakness, "自由表达用词简单。",
            [new EvidenceClaim("free_expression_log", freeLog.Id.ToString(), "aiScore", "<=", 80)],
            FindingConfidence.Low);
        var forged = truthful with
        {
            Statement = "伪造引用。",
            Evidence = [new EvidenceClaim("free_expression_log", Guid.NewGuid().ToString(), "aiScore", "<=", 80)]
        };
        var tampered = truthful with
        {
            Statement = "篡改数值。",
            Evidence = [new EvidenceClaim("free_expression_log", freeLog.Id.ToString(), "aiScore", "<=", 60)]
        };
        var unknownMetric = truthful with
        {
            Statement = "未知指标。",
            Evidence = [new EvidenceClaim("free_expression_log", freeLog.Id.ToString(), "grammar", "<=", 80)]
        };

        var results = await verifier.VerifyAsync(user.Id, null, [truthful, forged, tampered, unknownMetric], CancellationToken.None);

        Assert.Equal(FindingVerification.Verified, results[0].Verification);
        Assert.Equal(FindingVerification.Questioned, results[1].Verification);
        Assert.Contains("不存在", results[1].Note);
        Assert.Equal(FindingVerification.Questioned, results[2].Verification);
        Assert.Contains("不属实", results[2].Note);
        Assert.Equal(FindingVerification.Questioned, results[3].Verification);
        Assert.Contains("未知指标", results[3].Note);

        // 放宽档同步：free_expression_log 条数不足（medium 需 ≥2 实际 1）→ 降 low 标 Verified 注「初步判断」
        var thin = truthful with { Confidence = FindingConfidence.Medium };
        var relaxed = await verifier.VerifyAsync(user.Id, null, [thin], CancellationToken.None, relaxedColdStart: true);
        Assert.Equal(FindingVerification.Verified, relaxed[0].Verification);
        Assert.Equal(FindingConfidence.Low, relaxed[0].Draft.Confidence);
        Assert.Contains("初步判断", relaxed[0].Note);
    }

    // ── 数据播种 ─────────────────────────────────────────

    private static async Task<User> SeedUserAsync(ApplicationDbContext db, string name, int registeredDaysAgo)
    {
        var user = new User
        {
            DisplayName = $"{name}-{Guid.NewGuid():N}",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-registeredDaysAgo)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<List<SentenceLog>> SeedSentenceLogsAsync(ApplicationDbContext db, Guid userId, int count)
    {
        var logs = Enumerable.Range(0, count).Select(index => new SentenceLog
        {
            UserId = userId,
            TargetWord = $"word{index}",
            Scene = "free",
            UserSentence = $"Sentence {index} for cold start.",
            GrammarScore = 4,
            NaturalScore = 4,
            VocabularyScore = 4,
            RelevanceScore = 4
        }).ToList();
        db.SentenceLogs.AddRange(logs);
        await db.SaveChangesAsync();
        return logs;
    }

    private static async Task SeedFreeExpressionsAsync(ApplicationDbContext db, Guid userId, int count)
    {
        db.FreeExpressionLogs.AddRange(Enumerable.Range(0, count).Select(index => new FreeExpressionLog
        {
            UserId = userId,
            UserText = $"Free expression {index} for cold start.",
            AiScore = 80
        }));
        await db.SaveChangesAsync();
    }

    private static async Task SeedAnnotatedWordAsync(ApplicationDbContext db, string salt)
    {
        var word = new Word
        {
            Lemma = $"coldword-{salt}-{Guid.NewGuid():N}",
            Meanings = ["词"],
            CefrLevel = CefrLevel.A2,
            DifficultyLevel = DifficultyLevel.Basic
        };
        word.Scenarios.Add(new WordScenario { WordId = word.Id, ScenarioKey = "dining_out" });
        db.Words.Add(word);
        await db.SaveChangesAsync();
    }

    // ── 服务装配 ─────────────────────────────────────────

    private sealed class StubProfilerLlm(Func<IReadOnlyList<ProfileFindingDraft>> drafts) : ILLMProvider
    {
        public Task<WeaknessProfileResponse> GenerateWeaknessProfileAsync(WeaknessProfileRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new WeaknessProfileResponse(drafts().ToList()));

        public Task<DifficultyRating> RateDifficultyAsync(ItemRatingRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<DefinitionResponse> GetDefinitionAsync(DefinitionRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<SentenceRatingResponse> RateSentenceAsync(SentenceRatingRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<VocabExtractResponse> ExtractVocabAsync(VocabExtractRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<CommentReplyResponse> ReplyToCommentAsync(CommentReplyRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ScenarioAnnotationResponse> AnnotateScenarioAsync(ScenarioAnnotationRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<BottleneckInsightResponse> GenerateBottleneckInsightAsync(BottleneckInsightRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    private sealed class StubLlmFactory(ILLMProvider provider) : IUserLlmProviderFactory
    {
        public Task<ILLMProvider> GetForUserAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(provider);
    }

    /// <summary>捕获 Profiler 请求的 stub：验证聚合输入而非草稿内容。</summary>
    private sealed class CapturingProfilerLlm : ILLMProvider
    {
        public WeaknessProfileRequest? LastRequest { get; private set; }

        public Task<WeaknessProfileResponse> GenerateWeaknessProfileAsync(WeaknessProfileRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new WeaknessProfileResponse([]));
        }

        public Task<DifficultyRating> RateDifficultyAsync(ItemRatingRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<DefinitionResponse> GetDefinitionAsync(DefinitionRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<SentenceRatingResponse> RateSentenceAsync(SentenceRatingRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<VocabExtractResponse> ExtractVocabAsync(VocabExtractRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<CommentReplyResponse> ReplyToCommentAsync(CommentReplyRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ScenarioAnnotationResponse> AnnotateScenarioAsync(ScenarioAnnotationRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<BottleneckInsightResponse> GenerateBottleneckInsightAsync(BottleneckInsightRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
