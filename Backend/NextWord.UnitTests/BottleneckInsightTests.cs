using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Domain.Services;
using NextWord.Infrastructure.Background;
using NextWord.Infrastructure.Data;
using NextWord.Infrastructure.Services;

namespace NextWord.UnitTests;

/// <summary>
/// T-007 瓶颈性质洞察 + 重规划触发（真实 PG）：
/// 指标筛查三类信号触发/不误触发；InsightAgent 持久化带证据引用（编造 id 机械过滤）；
/// 性质变化 → 重规划（画像重生成 + force Planner 入队），性质未变 → 只记录；
/// 未触发零 LLM 调用（计数桩）；每周兜底为活跃存量用户入队 force Planner（同周幂等）；
/// force 重规划同日原地重建 Plan。
/// </summary>
public class BottleneckInsightTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // ── 第一层：指标筛查 ─────────────────────────────────────

    [Fact]
    public async Task Plateau_triggers_for_flat_scores_and_not_for_improving()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var screening = new BottleneckScreeningService(db);

        // 平台期：10 次产出四维均分恒 3.0、10 天内 → 触发
        var flatUser = await SeedUserAsync(db, "screen-plateau");
        await SeedSentenceLogsAsync(db, flatUser.Id, 10, index => 3, index => $"I like word{index} very much.");
        var signals = await screening.ScreenAsync(flatUser.Id, CancellationToken.None);
        Assert.Contains(BottleneckSignal.Plateau, signals);

        // 稳步提升：均分 1→3 斜率远超阈值 → 不触发
        var improvingUser = await SeedUserAsync(db, "screen-improving");
        await SeedSentenceLogsAsync(db, improvingUser.Id, 10, index => 1 + index / 3, index => $"I like word{index} very much.");
        var improvingSignals = await screening.ScreenAsync(improvingUser.Id, CancellationToken.None);
        Assert.DoesNotContain(BottleneckSignal.Plateau, improvingSignals);

        // 样本不足（<10 次）→ 不触发
        var fewUser = await SeedUserAsync(db, "screen-few");
        await SeedSentenceLogsAsync(db, fewUser.Id, 5, _ => 3, index => $"Short {index}.");
        var fewSignals = await screening.ScreenAsync(fewUser.Id, CancellationToken.None);
        Assert.DoesNotContain(BottleneckSignal.Plateau, fewSignals);
    }

    [Fact]
    public async Task Avoidance_triggers_on_declining_connectives_and_not_when_stable()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var screening = new BottleneckScreeningService(db);

        // 回避模式：前 6 句复杂连接密集（每句 ≥2 个），后 6 句全是简单句 → 触发
        var avoidUser = await SeedUserAsync(db, "screen-avoid");
        await SeedSentenceLogsAsync(
            db,
            avoidUser.Id,
            12,
            index => 2 + (index % 3), // 分数爬升，避免与平台期信号纠缠的解读干扰
            index => index < 6
                ? "I stayed home because it was raining while my friend waited outside."
                : "I went home. It rained. My friend waited.");
        var signals = await screening.ScreenAsync(avoidUser.Id, CancellationToken.None);
        Assert.Contains(BottleneckSignal.Avoidance, signals);

        // 稳定使用复杂连接 → 不触发
        var stableUser = await SeedUserAsync(db, "screen-stable");
        await SeedSentenceLogsAsync(
            db,
            stableUser.Id,
            12,
            index => 2 + (index % 3),
            _ => "I stayed home because it was raining while my friend waited outside.");
        var stableSignals = await screening.ScreenAsync(stableUser.Id, CancellationToken.None);
        Assert.DoesNotContain(BottleneckSignal.Avoidance, stableSignals);
    }

    [Fact]
    public async Task Safe_word_triggers_when_plan_targets_never_enter_free_production()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var screening = new BottleneckScreeningService(db);

        // 安全词策略：生效 Plan 造句目标 targetalpha/targetbeta，3 篇自由产出都绕开 → 触发
        var safeUser = await SeedUserAsync(db, "screen-safeword");
        await SeedActivePlanAsync(db, safeUser.Id, ["targetalpha", "targetbeta"]);
        await SeedFreeExpressionsAsync(db, safeUser.Id, ["I enjoy my daily routine.", "We talked about movies.", "She cooks dinner every night."]);
        var signals = await screening.ScreenAsync(safeUser.Id, CancellationToken.None);
        Assert.Contains(BottleneckSignal.SafeWord, signals);

        // 有一篇用了目标词 → 不触发
        var usingUser = await SeedUserAsync(db, "screen-using");
        await SeedActivePlanAsync(db, usingUser.Id, ["targetgamma"]);
        await SeedFreeExpressionsAsync(db, usingUser.Id, ["I enjoy my daily routine.", "The targetgamma idea works well.", "She cooks dinner every night."]);
        var usingSignals = await screening.ScreenAsync(usingUser.Id, CancellationToken.None);
        Assert.DoesNotContain(BottleneckSignal.SafeWord, usingSignals);

        // 自由产出样本不足（<3 篇）→ 无从判定，不触发
        var thinUser = await SeedUserAsync(db, "screen-thin");
        await SeedActivePlanAsync(db, thinUser.Id, ["targetdelta"]);
        await SeedFreeExpressionsAsync(db, thinUser.Id, ["Only one free text."]);
        var thinSignals = await screening.ScreenAsync(thinUser.Id, CancellationToken.None);
        Assert.DoesNotContain(BottleneckSignal.SafeWord, thinSignals);
    }

    // ── T-012 安全词误触发修复：窗口从 Plan.CreatedAt 起算 + 24h 宽限期 ──

    [Fact]
    public async Task Safe_word_ignores_free_production_before_plan_creation()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var screening = new BottleneckScreeningService(db);

        // 复现 T-012：全部自由产出都早于 Plan 创建（写的是旧目标词时代的内容，不含新目标词）——
        // 旧口径按「生效日 00:00」起算会把它们计入 → 误判出现率为 0；修复后窗口从 CreatedAt 起算 → 样本不足不触发
        var user = await SeedUserAsync(db, "t012-preplan");
        var planCreatedAt = DateTimeOffset.UtcNow.AddHours(-30);
        await SeedActivePlanAsync(db, user.Id, ["targetnew"], createdAt: planCreatedAt);
        await SeedFreeExpressionsAsync(
            db,
            user.Id,
            ["I enjoy my daily routine.", "We talked about movies.", "She cooks dinner every night.", "He walks to work."],
            timestamp: planCreatedAt.AddHours(-2));

        var signals = await screening.ScreenAsync(user.Id, CancellationToken.None);
        Assert.DoesNotContain(BottleneckSignal.SafeWord, signals);
    }

    [Fact]
    public async Task Safe_word_skipped_within_24h_grace_period_of_new_plan()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var screening = new BottleneckScreeningService(db);

        // 新 Plan 创建未满 24h：即使窗口内 ≥3 篇产出都不含目标词，也不做安全词判定（宽限期）
        var user = await SeedUserAsync(db, "t012-grace");
        await SeedActivePlanAsync(db, user.Id, ["targetfresh"], createdAt: DateTimeOffset.UtcNow.AddHours(-2));
        await SeedFreeExpressionsAsync(db, user.Id, ["I enjoy my daily routine.", "We talked about movies.", "She cooks dinner every night."]);

        var signals = await screening.ScreenAsync(user.Id, CancellationToken.None);
        Assert.DoesNotContain(BottleneckSignal.SafeWord, signals);
    }

    [Fact]
    public async Task Safe_word_still_triggers_after_grace_period_when_targets_absent()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var screening = new BottleneckScreeningService(db);

        // 宽限期后：Plan 创建后的 ≥3 篇产出确实都不含目标词 → 仍正确触发
        var user = await SeedUserAsync(db, "t012-after-grace");
        var planCreatedAt = DateTimeOffset.UtcNow.AddHours(-30);
        await SeedActivePlanAsync(db, user.Id, ["targetlate"], createdAt: planCreatedAt);
        await SeedFreeExpressionsAsync(
            db,
            user.Id,
            ["I enjoy my daily routine.", "We talked about movies.", "She cooks dinner every night."],
            timestamp: planCreatedAt.AddHours(5));

        var signals = await screening.ScreenAsync(user.Id, CancellationToken.None);
        Assert.Contains(BottleneckSignal.SafeWord, signals);
    }

    // ── 第二层：InsightAgent ─────────────────────────────────

    [Fact]
    public async Task Insight_persists_with_filtered_evidence_and_triggers_replan_on_nature_change()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, "insight-change");
        var logs = await SeedSentenceLogsAsync(db, user.Id, 6, _ => 2, index => $"Broken sentence {index}.");
        var forgedId = Guid.NewGuid();
        var llm = new CountingInsightLlm(_ => new BottleneckInsightResponse(
            BottleneckNature.GrammarErrors,
            "语法错误频繁，时态与单复数混乱。",
            [logs[0].Id, logs[1].Id, forgedId]));
        var jobs = new RecordingBackgroundJobs();
        var service = CreateInsightService(db, llm, jobs);

        var insight = await service.GenerateAsync(user.Id, [BottleneckSignal.Plateau], CancellationToken.None);

        Assert.NotNull(insight);
        Assert.Equal(BottleneckNature.GrammarErrors, insight.Nature);
        Assert.Equal("plateau", insight.Signals);
        // 证据引用机械过滤：编造的 id 被丢弃，只留真实 SentenceLog
        var evidence = JsonSerializer.Deserialize<List<Guid>>(insight!.EvidenceJson, JsonOptions)!;
        Assert.Equal([logs[0].Id, logs[1].Id], evidence);
        Assert.DoesNotContain(forgedId, evidence);
        // 首次发现瓶颈 = 性质已变 → 触发重规划：画像重生成（AssessmentId 空）+ force Planner 入队
        Assert.True(insight.ReplanTriggered);
        Assert.Equal(1, await db.WeaknessProfiles.CountAsync(item => item.UserId == user.Id && item.AssessmentId == null));
        var enqueue = Assert.Single(jobs.Enqueued);
        Assert.Equal(PlannerWorker.JobType, enqueue.JobType);
        Assert.Equal($"planner:replan:{user.Id}:{DateTimeOffset.UtcNow:yyyyMMdd}", enqueue.Key);
        Assert.Contains("\"force\":true", enqueue.PayloadJson);

        // 同日幂等：重复触发不再调 LLM、不产生新行
        var again = await service.GenerateAsync(user.Id, [BottleneckSignal.Plateau], CancellationToken.None);
        Assert.Null(again);
        Assert.Equal(1, llm.InsightCalls);
        Assert.Equal(1, await db.BottleneckInsights.CountAsync(item => item.UserId == user.Id));
    }

    [Fact]
    public async Task Unchanged_nature_records_without_replan()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, "insight-same");
        await SeedSentenceLogsAsync(db, user.Id, 6, _ => 2, index => $"Broken sentence {index}.");
        // 上一条洞察：同性质（语法错误多），昨天记录
        db.BottleneckInsights.Add(new BottleneckInsight
        {
            UserId = user.Id,
            Nature = BottleneckNature.GrammarErrors,
            Signals = "plateau",
            Statement = "语法错误频繁。",
            EvidenceJson = "[]",
            ReplanTriggered = true,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();

        var llm = new CountingInsightLlm(request => new BottleneckInsightResponse(
            BottleneckNature.GrammarErrors,
            "语法错误仍然频繁。",
            request.Productions.Take(1).Select(sample => sample.Id).ToList()));
        var jobs = new RecordingBackgroundJobs();
        var service = CreateInsightService(db, llm, jobs);

        var insight = await service.GenerateAsync(user.Id, [BottleneckSignal.Plateau], CancellationToken.None);

        // 性质未变 → 只记录：不重生成画像、不入队 Planner
        Assert.NotNull(insight);
        Assert.False(insight!.ReplanTriggered);
        Assert.Empty(jobs.Enqueued);
        Assert.Equal(0, await db.WeaknessProfiles.CountAsync(item => item.UserId == user.Id));
        Assert.Equal(2, await db.BottleneckInsights.CountAsync(item => item.UserId == user.Id));
    }

    [Fact]
    public async Task Untriggered_user_has_zero_llm_calls()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        // 正常用户：分数稳步提升 + 复杂连接稳定 + 无 Plan → 三类信号全不触发
        var user = await SeedUserAsync(db, "insight-zero");
        await SeedSentenceLogsAsync(
            db,
            user.Id,
            12,
            index => 1 + (index % 4),
            _ => "I stayed home because it was raining while my friend waited outside.");
        var llm = new CountingInsightLlm(_ => throw new InvalidOperationException("must not be called"));
        var jobs = new RecordingBackgroundJobs();

        var screening = new BottleneckScreeningService(db);
        var signals = await screening.ScreenAsync(user.Id, CancellationToken.None);
        Assert.Empty(signals);

        // 模拟日快照筛查链路：无信号则不入队、不调 LLM
        if (signals.Count > 0)
        {
            var service = CreateInsightService(db, llm, jobs);
            await service.GenerateAsync(user.Id, signals, CancellationToken.None);
        }

        Assert.Equal(0, llm.InsightCalls);
        Assert.Empty(jobs.Enqueued);
        Assert.Equal(0, await db.BottleneckInsights.CountAsync(item => item.UserId == user.Id));
    }

    // ── 第三层：重规划 ───────────────────────────────────────

    [Fact]
    public async Task Force_replan_rebuilds_todays_plan_in_place()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, "replan-force");
        var plans = new LearningPlanService(db, new ScoreProfileService(db, new ScoreMappingService(new ScoreMappingOptions())));

        var plan = await plans.GenerateAsync(user.Id, CancellationToken.None);
        var originalContent = plan.ContentJson;
        var originalCreatedAt = plan.CreatedAt;

        // 非 force：同日幂等直接返回
        var again = await plans.GenerateAsync(user.Id, CancellationToken.None);
        Assert.Equal(plan.Id, again.Id);
        Assert.Equal(originalContent, again.ContentJson);

        // force：同一行原地重建内容（(UserId, StartDate) 唯一不破）、CreatedAt 刷新
        var forced = await plans.GenerateAsync(user.Id, CancellationToken.None, force: true);
        Assert.Equal(plan.Id, forced.Id);
        Assert.Equal(1, await db.LearningPlans.CountAsync(item => item.UserId == user.Id));
        var reloaded = await db.LearningPlans.AsNoTracking().SingleAsync(item => item.Id == plan.Id);
        Assert.True(reloaded.CreatedAt > originalCreatedAt);
    }

    [Fact]
    public async Task Weekly_fallback_enqueues_force_planner_for_active_users_once_per_week()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var active1 = await SeedUserAsync(db, "weekly-active-1", assessed: true);
        var active2 = await SeedUserAsync(db, "weekly-active-2", assessed: true);
        var inactive = await SeedUserAsync(db, "weekly-inactive", assessed: false);
        var jobs = new BackgroundJobService(db);

        var now = DateTimeOffset.UtcNow;
        var enqueued = await WeeklyReplanWorker.EnqueueWeeklyReplanAsync(db, jobs, now, CancellationToken.None);

        // 共享库中还有其他测试的活跃用户，只断言本测试用户的任务
        Assert.True(enqueued >= 2);
        var week = $"{System.Globalization.ISOWeek.GetYear(now.UtcDateTime)}-W{System.Globalization.ISOWeek.GetWeekOfYear(now.UtcDateTime):00}";
        var keys = new[] { active1, active2 }
            .Select(id => $"planner:weekly:{id.Id}:{week}")
            .ToList();
        var rows = await db.BackgroundJobs.AsNoTracking()
            .Where(job => keys.Contains(job.IdempotencyKey))
            .ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, job =>
        {
            Assert.Equal(PlannerWorker.JobType, job.JobType);
            Assert.Contains("\"force\":true", job.PayloadJson);
        });
        Assert.Contains(rows, job => job.PayloadJson.Contains(active1.Id.ToString()));
        Assert.Contains(rows, job => job.PayloadJson.Contains(active2.Id.ToString()));
        // 未完成初测的存量用户不入队
        Assert.Equal(0, await db.BackgroundJobs.CountAsync(job => job.IdempotencyKey.Contains(inactive.Id.ToString())));

        // 同周重复运行幂等：本测试用户的任务不重复入队（共享库并行测试可能新增其他活跃用户，计数不归零）
        await WeeklyReplanWorker.EnqueueWeeklyReplanAsync(db, jobs, now, CancellationToken.None);
        Assert.Equal(2, await db.BackgroundJobs.CountAsync(job => keys.Contains(job.IdempotencyKey)));
    }

    [Fact]
    public async Task Profile_regeneration_without_assessment_is_idempotent_per_day()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, "profile-regen");
        await SeedSentenceLogsAsync(db, user.Id, 3, _ => 3, index => $"Sentence {index}.");
        var llm = new CountingInsightLlm(_ => throw new InvalidOperationException("insight not used here"));
        var profiler = new WeaknessProfiler(db, new StubLlmFactory(llm));
        var profiles = new WeaknessProfileService(db, profiler, new FindingVerifier(db));

        var first = await profiles.GenerateAsync(user.Id, null, CancellationToken.None);
        var second = await profiles.GenerateAsync(user.Id, null, CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await db.WeaknessProfiles.CountAsync(item => item.UserId == user.Id && item.AssessmentId == null));
        Assert.Equal(1, llm.ProfileCalls);
    }

    // ── 解析容错 ─────────────────────────────────────────────

    [Fact]
    public void Parser_tolerates_pipe_separated_nature_echoes()
    {
        var id = Guid.NewGuid();
        var content = $$"""
        {
          "nature": "grammar_errors|monotonous_expression",
          "statement": "语法错误频繁。",
          "evidenceLogIds": ["{{id}}", "not-a-guid"]
        }
        """;

        var response = LlmResponseParser.ParseBottleneckInsight(content);

        Assert.Equal(BottleneckNature.GrammarErrors, response.Nature);
        Assert.Equal([id], response.EvidenceLogIds);
        Assert.Throws<InvalidOperationException>(() =>
            LlmResponseParser.ParseBottleneckInsight("""{ "nature": "不认识的性质", "statement": "x", "evidenceLogIds": [] }"""));
    }

    // ── 数据播种 ─────────────────────────────────────────────

    private static async Task<User> SeedUserAsync(ApplicationDbContext db, string name, bool assessed = true)
    {
        var user = new User { DisplayName = $"{name}-{Guid.NewGuid():N}" };
        db.Users.Add(user);
        db.UserProgress.Add(new UserProgress
        {
            UserId = user.Id,
            HasCompletedInitialAssessment = assessed,
            CefrDisplay = "A2",
            VocabularyScore = 50
        });
        await db.SaveChangesAsync();
        return user;
    }

    /// <summary>按时间正序播种造句留痕（每天一条，index 递增 = 时间递增）。</summary>
    private static async Task<List<SentenceLog>> SeedSentenceLogsAsync(
        ApplicationDbContext db, Guid userId, int count, Func<int, int> score, Func<int, string> text)
    {
        var logs = Enumerable.Range(0, count)
            .Select(index => new SentenceLog
            {
                UserId = userId,
                TargetWord = $"word{index}",
                Scene = "life",
                UserSentence = text(index),
                GrammarScore = score(index),
                NaturalScore = score(index),
                VocabularyScore = score(index),
                RelevanceScore = score(index),
                Timestamp = DateTimeOffset.UtcNow.AddDays(index - count)
            })
            .ToList();
        db.SentenceLogs.AddRange(logs);
        await db.SaveChangesAsync();
        return logs;
    }

    private static async Task SeedActivePlanAsync(
        ApplicationDbContext db, Guid userId, string[] sentenceTargets, DateTimeOffset? createdAt = null)
    {
        var content = new LearningPlanContent(
            ["dining_out"],
            [],
            [],
            [new LearningPlanDay([], [], sentenceTargets),
             ..Enumerable.Range(0, 6).Select(_ => new LearningPlanDay([], [], sentenceTargets))]);
        db.LearningPlans.Add(new LearningPlan
        {
            UserId = userId,
            // 起始日拨回两天前：Plan 仍生效，且自由产出（数小时前）必然落在 Plan 生效窗口内（防跨午夜边界）；
            // 创建时间默认两天前（越过 T-012 的 24h 宽限期，安全词判定才会生效）
            StartDate = LearningPlanService.Today().AddDays(-2),
            ContentJson = JsonSerializer.Serialize(content, JsonOptions),
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow.AddDays(-2)
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedFreeExpressionsAsync(ApplicationDbContext db, Guid userId, string[] texts, DateTimeOffset? timestamp = null)
    {
        db.FreeExpressionLogs.AddRange(texts.Select((text, index) => new FreeExpressionLog
        {
            UserId = userId,
            UserText = text,
            AiScore = 60,
            Timestamp = (timestamp ?? DateTimeOffset.UtcNow).AddHours(-index)
        }));
        await db.SaveChangesAsync();
    }

    // ── 服务装配 ─────────────────────────────────────────────

    private static BottleneckInsightService CreateInsightService(
        ApplicationDbContext db, CountingInsightLlm llm, RecordingBackgroundJobs jobs)
    {
        var profiler = new WeaknessProfiler(db, new StubLlmFactory(llm));
        var profiles = new WeaknessProfileService(db, profiler, new FindingVerifier(db));
        return new BottleneckInsightService(
            db,
            new StubLlmFactory(llm),
            profiles,
            jobs,
            NullLogger<BottleneckInsightService>.Instance);
    }

    /// <summary>计数桩：洞察/画像调用分别计数，用于「未触发零 LLM」与幂等断言。</summary>
    private sealed class CountingInsightLlm(Func<BottleneckInsightRequest, BottleneckInsightResponse> respond) : ILLMProvider
    {
        public int InsightCalls { get; private set; }
        public int ProfileCalls { get; private set; }

        public Task<BottleneckInsightResponse> GenerateBottleneckInsightAsync(BottleneckInsightRequest request, CancellationToken cancellationToken)
        {
            InsightCalls++;
            return Task.FromResult(respond(request));
        }

        public Task<WeaknessProfileResponse> GenerateWeaknessProfileAsync(WeaknessProfileRequest request, CancellationToken cancellationToken)
        {
            ProfileCalls++;
            // 画像重生成走真实 Profiler/Verifier：返回空草稿（无 Finding）即可验证编排与幂等
            return Task.FromResult(new WeaknessProfileResponse([]));
        }

        public Task<DifficultyRating> RateDifficultyAsync(ItemRatingRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<DefinitionResponse> GetDefinitionAsync(DefinitionRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<SentenceRatingResponse> RateSentenceAsync(SentenceRatingRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<VocabExtractResponse> ExtractVocabAsync(VocabExtractRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<CommentReplyResponse> ReplyToCommentAsync(CommentReplyRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ScenarioAnnotationResponse> AnnotateScenarioAsync(ScenarioAnnotationRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    private sealed class StubLlmFactory(ILLMProvider provider) : IUserLlmProviderFactory
    {
        public Task<ILLMProvider> GetForUserAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(provider);
    }

    /// <summary>记录入队调用（不落库），断言重规划触发。</summary>
    private sealed class RecordingBackgroundJobs : IBackgroundJobService
    {
        public List<(string JobType, string PayloadJson, string Key)> Enqueued { get; } = [];

        public Task<long> EnqueueAsync(string jobType, string payloadJson, string idempotencyKey, CancellationToken cancellationToken)
        {
            Enqueued.Add((jobType, payloadJson, idempotencyKey));
            return Task.FromResult((long)Enqueued.Count);
        }

        public Task ProcessPendingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
