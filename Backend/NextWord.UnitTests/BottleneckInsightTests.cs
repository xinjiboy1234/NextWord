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
/// T-007 瓶颈性质洞察 + 重规划触发（真实 PG）；T-033 信号口径 v2（DESIGN-insight-signals-v2）：
/// 指标筛查四类信号（平台期/回避/零起步/安全词）触发/不误触发；InsightAgent 持久化带证据引用（编造 id 机械过滤）；
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

        // 平台期：12 次产出四维均分恒 3.0、12 天内 → 触发（T-033：窗口 10→12）
        var flatUser = await SeedUserAsync(db, "screen-plateau");
        await SeedSentenceLogsAsync(db, flatUser.Id, 12, index => 3, index => $"I like word{index} very much.");
        var signals = await screening.ScreenAsync(flatUser.Id, CancellationToken.None);
        Assert.Contains(BottleneckSignal.Plateau, signals);

        // 稳步提升：均分 1→4 斜率远超阈值 → 不触发
        var improvingUser = await SeedUserAsync(db, "screen-improving");
        await SeedSentenceLogsAsync(db, improvingUser.Id, 12, index => 1 + index / 3, index => $"I like word{index} very much.");
        var improvingSignals = await screening.ScreenAsync(improvingUser.Id, CancellationToken.None);
        Assert.DoesNotContain(BottleneckSignal.Plateau, improvingSignals);

        // 样本不足（<12 次）→ 不触发
        var fewUser = await SeedUserAsync(db, "screen-few");
        await SeedSentenceLogsAsync(db, fewUser.Id, 5, _ => 3, index => $"Short {index}.");
        var fewSignals = await screening.ScreenAsync(fewUser.Id, CancellationToken.None);
        Assert.DoesNotContain(BottleneckSignal.Plateau, fewSignals);
    }

    [Fact]
    public async Task Plateau_relaxed_stddev_boundary()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var screening = new BottleneckScreeningService(db);

        // T-033 放宽（stdDev 0.5→1.0）边界（DESIGN-insight-signals-v2 §4.5，回文序列保证斜率=0）：
        // stdDev≈0.82 的平坦序列触发——菜鸟真实波动区间内；
        var calmScores = new[] { 2, 3, 4, 4, 3, 2, 2, 3, 4, 4, 3, 2 };
        var calmUser = await SeedUserAsync(db, "screen-plateau-calm");
        await SeedSentenceLogsAsync(db, calmUser.Id, 12, index => calmScores[index], index => $"Calm sentence {index} here.");
        var calmSignals = await screening.ScreenAsync(calmUser.Id, CancellationToken.None);
        Assert.Contains(BottleneckSignal.Plateau, calmSignals);

        // stdDev=1.5 的大起大落序列不触发——放宽不是放水
        var wildScores = new[] { 1, 4, 4, 1, 1, 4, 4, 1, 1, 4, 4, 1 };
        var wildUser = await SeedUserAsync(db, "screen-plateau-wild");
        await SeedSentenceLogsAsync(db, wildUser.Id, 12, index => wildScores[index], index => $"Wild sentence {index} here.");
        var wildSignals = await screening.ScreenAsync(wildUser.Id, CancellationToken.None);
        Assert.DoesNotContain(BottleneckSignal.Plateau, wildSignals);
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

        // T-033 相对基线：前半段率 > 0 即有基线，不设绝对下限——
        // 前 6 句只有 1 句用过 1 个连接词（率 1/6 ≈ 0.167，低于旧口径 0.3 基线），后 6 句恒 0 → 新口径仍触发
        var lowBaseUser = await SeedUserAsync(db, "screen-avoid-lowbase");
        await SeedSentenceLogsAsync(
            db,
            lowBaseUser.Id,
            12,
            index => 2 + (index % 3),
            index => index == 0
                ? "I stayed home because it rained."
                : "I went home early today.");
        var lowBaseSignals = await screening.ScreenAsync(lowBaseUser.Id, CancellationToken.None);
        Assert.Contains(BottleneckSignal.Avoidance, lowBaseSignals);

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

        // 从未用过复杂连接 → 不判回避（前半段率 0 无基线，交零起步信号覆盖）
        var neverUser = await SeedUserAsync(db, "screen-avoid-never");
        await SeedSentenceLogsAsync(
            db,
            neverUser.Id,
            12,
            index => 2 + (index % 3),
            index => $"I wrote a longer simple sentence number {index} to keep growing.");
        var neverSignals = await screening.ScreenAsync(neverUser.Id, CancellationToken.None);
        Assert.DoesNotContain(BottleneckSignal.Avoidance, neverSignals);
    }

    // ── T-033 零起步信号（DESIGN-insight-signals-v2 §2.3/§4.3）──

    [Fact]
    public async Task Cold_start_triggers_for_zero_connectives_and_flat_sentence_length()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var screening = new BottleneckScreeningService(db);

        // 近 10 次产出：复杂连接恒 0 + 句长恒定（4 词）+ 10 天内 → 触发
        var user = await SeedUserAsync(db, "screen-coldstart");
        await SeedSentenceLogsAsync(db, user.Id, 10, _ => 2, _ => "I like my cat.");
        var signals = await screening.ScreenAsync(user.Id, CancellationToken.None);
        Assert.Contains(BottleneckSignal.ColdStart, signals);
    }

    [Fact]
    public async Task Cold_start_not_triggered_when_sentence_length_grows()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var screening = new BottleneckScreeningService(db);

        // 连接恒 0 但句长有增长：后半段 9 词 > 前半段 4 词 × 1.1 → 不触发
        var user = await SeedUserAsync(db, "screen-coldstart-grow");
        await SeedSentenceLogsAsync(
            db,
            user.Id,
            10,
            _ => 2,
            index => index < 5 ? "I like my cat." : "I like my cat and my dog very much.");
        var signals = await screening.ScreenAsync(user.Id, CancellationToken.None);
        Assert.DoesNotContain(BottleneckSignal.ColdStart, signals);
    }

    [Fact]
    public async Task Cold_start_not_triggered_when_window_spans_over_30_days()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var screening = new BottleneckScreeningService(db);

        // 连接恒 0、句长不增，但 10 次产出每 5 天一篇、跨度 45 天 > 30 → 不持续活跃，不触发
        var user = await SeedUserAsync(db, "screen-coldstart-sparse");
        await SeedSentenceLogsAsync(db, user.Id, 10, _ => 2, _ => "I like my cat.", daysApart: 5);
        var signals = await screening.ScreenAsync(user.Id, CancellationToken.None);
        Assert.DoesNotContain(BottleneckSignal.ColdStart, signals);
    }

    [Fact]
    public void Cold_start_signal_wire_name_round_trips()
    {
        // 持久化/payload 走字符串 wire 名：新信号可序列化回读，旧数据不受影响
        Assert.Equal("cold_start", BottleneckSignal.ColdStart.ToWireName());
        Assert.True(BottleneckSignalNames.TryParse("cold_start", out var parsed));
        Assert.Equal(BottleneckSignal.ColdStart, parsed);
    }

    [Fact]
    public async Task Safe_word_triggers_when_plan_targets_never_enter_free_production()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var screening = new BottleneckScreeningService(db);

        // 安全词策略：生效 Plan 造句目标 targetalpha/targetbeta，最近 5 篇自由产出都绕开 → 触发（T-033：窗口 3→5 篇）
        var safeUser = await SeedUserAsync(db, "screen-safeword");
        await SeedActivePlanAsync(db, safeUser.Id, ["targetalpha", "targetbeta"]);
        await SeedFreeExpressionsAsync(db, safeUser.Id,
            ["I enjoy my daily routine.", "We talked about movies.", "She cooks dinner every night.", "He walks to work.", "They play chess on weekends."]);
        var signals = await screening.ScreenAsync(safeUser.Id, CancellationToken.None);
        Assert.Contains(BottleneckSignal.SafeWord, signals);

        // 有一篇用了目标词 → 不触发
        var usingUser = await SeedUserAsync(db, "screen-using");
        await SeedActivePlanAsync(db, usingUser.Id, ["targetgamma"]);
        await SeedFreeExpressionsAsync(db, usingUser.Id,
            ["I enjoy my daily routine.", "The targetgamma idea works well.", "She cooks dinner every night.", "He walks to work.", "They play chess on weekends."]);
        var usingSignals = await screening.ScreenAsync(usingUser.Id, CancellationToken.None);
        Assert.DoesNotContain(BottleneckSignal.SafeWord, usingSignals);

        // 自由产出样本不足（<5 篇，T-033 新窗口下限）→ 无从判定，不触发
        var thinUser = await SeedUserAsync(db, "screen-thin");
        await SeedActivePlanAsync(db, thinUser.Id, ["targetdelta"]);
        await SeedFreeExpressionsAsync(db, thinUser.Id,
            ["Only one free text.", "Another short text.", "A third one here.", "And a fourth."]);
        var thinSignals = await screening.ScreenAsync(thinUser.Id, CancellationToken.None);
        Assert.DoesNotContain(BottleneckSignal.SafeWord, thinSignals);
    }

    [Fact]
    public async Task Safe_word_window_counts_recent_free_production_across_plan_cycles()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var screening = new BottleneckScreeningService(db);

        // T-033 新窗口（DESIGN-insight-signals-v2 §2.4/§4.4）：最近 5 篇自由产出、跨计划周期累计——
        // 3 篇早于当前 Plan 创建、2 篇晚于（旧口径自 Plan.CreatedAt 起算只有 2 篇 < 下限不触发），
        // 5 篇都不含目标词 → 新口径触发，不再被 7 天计划周期卡死
        var user = await SeedUserAsync(db, "t033-crossplan");
        var planCreatedAt = DateTimeOffset.UtcNow.AddHours(-30);
        await SeedActivePlanAsync(db, user.Id, ["targetcross"], createdAt: planCreatedAt);
        await SeedFreeExpressionsAsync(
            db,
            user.Id,
            ["I enjoy my daily routine.", "We talked about movies.", "She cooks dinner every night.", "He walks to work.", "They play chess on weekends."],
            timestamps:
            [
                planCreatedAt.AddHours(-26), planCreatedAt.AddHours(-14), planCreatedAt.AddHours(-2),
                planCreatedAt.AddHours(10), planCreatedAt.AddHours(22)
            ]);

        var signals = await screening.ScreenAsync(user.Id, CancellationToken.None);
        Assert.Contains(BottleneckSignal.SafeWord, signals);
    }

    [Fact]
    public async Task Safe_word_phrase_target_matches_content_words_not_stopwords()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var screening = new BottleneckScreeningService(db);

        // T-033 短语匹配口径（§2.4）："see eye to eye" 拆词去停用词 → 内容词 {see, eye}，全部同现才算用过。
        // 极端一：只含功能词 "to"（每篇都有）不算用过 → 触发
        var stopwordUser = await SeedUserAsync(db, "t033-phrase-stopword");
        await SeedActivePlanAsync(db, stopwordUser.Id, ["see eye to eye"]);
        await SeedFreeExpressionsAsync(db, stopwordUser.Id,
            ["I want to go to the park.", "She likes to read at night.", "We plan to travel to Japan.", "He tries to call to apologize.", "They hope to win the game."]);
        var stopwordSignals = await screening.ScreenAsync(stopwordUser.Id, CancellationToken.None);
        Assert.Contains(BottleneckSignal.SafeWord, stopwordSignals);

        // 极端二：整串不必原样出现，内容词同现即算用过 → 不触发
        var usedUser = await SeedUserAsync(db, "t033-phrase-used");
        await SeedActivePlanAsync(db, usedUser.Id, ["see eye to eye"]);
        await SeedFreeExpressionsAsync(db, usedUser.Id,
            ["I want to go to the park.", "We finally see eye to eye on this plan.", "She likes to read at night.", "He walks to work.", "They play chess on weekends."]);
        var usedSignals = await screening.ScreenAsync(usedUser.Id, CancellationToken.None);
        Assert.DoesNotContain(BottleneckSignal.SafeWord, usedSignals);

        // 边界：内容词只中其一（有 see 无 eye）→ 不算用过 → 触发
        var partialUser = await SeedUserAsync(db, "t033-phrase-partial");
        await SeedActivePlanAsync(db, partialUser.Id, ["see eye to eye"]);
        await SeedFreeExpressionsAsync(db, partialUser.Id,
            ["I want to go to the park.", "I see a bird in the tree.", "She likes to read at night.", "He walks to work.", "They play chess on weekends."]);
        var partialSignals = await screening.ScreenAsync(partialUser.Id, CancellationToken.None);
        Assert.Contains(BottleneckSignal.SafeWord, partialSignals);
    }

    // ── T-012 宽限期保留 + T-033 窗口口径变更（按篇数不按天数，跨计划周期累计）──

    [Fact]
    public async Task Safe_word_counts_free_production_before_plan_creation_under_new_window()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var screening = new BottleneckScreeningService(db);

        // T-033 口径变更（原 T-012 用例反转）：窗口改为最近 5 篇自由产出，不再从 Plan.CreatedAt 起算——
        // 5 篇近期产出虽都早于 Plan 创建也计入样本，不含目标词 → 触发；
        // 新 Plan 的防误判由保留的 24h 宽限期承担（见下条用例）
        var user = await SeedUserAsync(db, "t012-preplan");
        var planCreatedAt = DateTimeOffset.UtcNow.AddHours(-30);
        await SeedActivePlanAsync(db, user.Id, ["targetnew"], createdAt: planCreatedAt);
        await SeedFreeExpressionsAsync(
            db,
            user.Id,
            ["I enjoy my daily routine.", "We talked about movies.", "She cooks dinner every night.", "He walks to work.", "They play chess on weekends."],
            timestamp: planCreatedAt.AddHours(-2));

        var signals = await screening.ScreenAsync(user.Id, CancellationToken.None);
        Assert.Contains(BottleneckSignal.SafeWord, signals);
    }

    [Fact]
    public async Task Safe_word_skipped_within_24h_grace_period_of_new_plan()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var screening = new BottleneckScreeningService(db);

        // 新 Plan 创建未满 24h：即使最近 5 篇产出都不含目标词，也不做安全词判定（宽限期保留，T-012）
        var user = await SeedUserAsync(db, "t012-grace");
        await SeedActivePlanAsync(db, user.Id, ["targetfresh"], createdAt: DateTimeOffset.UtcNow.AddHours(-2));
        await SeedFreeExpressionsAsync(db, user.Id,
            ["I enjoy my daily routine.", "We talked about movies.", "She cooks dinner every night.", "He walks to work.", "They play chess on weekends."]);

        var signals = await screening.ScreenAsync(user.Id, CancellationToken.None);
        Assert.DoesNotContain(BottleneckSignal.SafeWord, signals);
    }

    [Fact]
    public async Task Safe_word_still_triggers_after_grace_period_when_targets_absent()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var screening = new BottleneckScreeningService(db);

        // 宽限期后：最近 5 篇产出确实都不含目标词 → 仍正确触发（T-033：样本下限 3→5 篇）
        var user = await SeedUserAsync(db, "t012-after-grace");
        var planCreatedAt = DateTimeOffset.UtcNow.AddHours(-30);
        await SeedActivePlanAsync(db, user.Id, ["targetlate"], createdAt: planCreatedAt);
        await SeedFreeExpressionsAsync(
            db,
            user.Id,
            ["I enjoy my daily routine.", "We talked about movies.", "She cooks dinner every night.", "He walks to work.", "They play chess on weekends."],
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

    /// <summary>按时间正序播种造句留痕（默认每天一条，index 递增 = 时间递增；daysApart 可调间隔模拟稀疏活跃）。</summary>
    private static async Task<List<SentenceLog>> SeedSentenceLogsAsync(
        ApplicationDbContext db, Guid userId, int count, Func<int, int> score, Func<int, string> text, double daysApart = 1)
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
                Timestamp = DateTimeOffset.UtcNow.AddDays((index - count) * daysApart)
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

    /// <summary>播种自由产出。timestamps 显式给定逐篇时间（与 texts 等长）；否则以 timestamp 为末篇、逐篇往前推 1 小时。</summary>
    private static async Task SeedFreeExpressionsAsync(
        ApplicationDbContext db, Guid userId, string[] texts, DateTimeOffset? timestamp = null, IReadOnlyList<DateTimeOffset>? timestamps = null)
    {
        db.FreeExpressionLogs.AddRange(texts.Select((text, index) => new FreeExpressionLog
        {
            UserId = userId,
            UserText = text,
            AiScore = 60,
            Timestamp = timestamps?[index] ?? (timestamp ?? DateTimeOffset.UtcNow).AddHours(-index)
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
