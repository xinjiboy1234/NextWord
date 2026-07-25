using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Domain.Scenarios;
using NextWord.Domain.Services;
using NextWord.Infrastructure.Data;
using NextWord.Infrastructure.Services;

namespace NextWord.UnitTests;

/// <summary>
/// T-006 PlannerWorker + 每日内容来源切换（真实 PG）：
/// 只消费 Verified Finding 排主攻场景；接触词 ≤20% 且全超带；同日幂等；
/// 无 Plan / 过期（&gt;7 天）回退难度带；阅读推荐按主攻场景选文；造句出题用 Plan 目标。
/// 水平带 = CEFR（与测评词池口径一致）：用户 A2，带内词 CefrLevel=A2，接触词 CefrLevel&gt;A2。
/// </summary>
public class LearningPlanTests
{
    private const CefrLevel UserBand = CefrLevel.A2;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Generate_uses_only_verified_findings_and_is_idempotent()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserWithBandAsync(db, "plan-verified");
        await SeedWordPoolAsync(db, "verified");
        var (verifiedId, questionedId, skillId) = await SeedProfileAsync(db, user.Id);
        var service = CreatePlanService(db);

        var plan = await service.GenerateAsync(user.Id, CancellationToken.None);

        var content = JsonSerializer.Deserialize<LearningPlanContent>(plan.ContentJson, JsonOptions)!;
        // 主攻场景来自 Verified 场景 weakness Finding；存疑条目与其他维度不进生成依据
        Assert.Equal(["dining_out"], content.FocusScenarios);
        Assert.Equal([verifiedId], content.SourceFindingIds);
        Assert.DoesNotContain(questionedId, content.SourceFindingIds);
        Assert.DoesNotContain(skillId, content.SourceFindingIds);

        // 7 日计划：每天接触词 ≤20%（10 × 0.2 = 2）且全部超带；词队列只来自主攻场景或 core 桶
        Assert.Equal(7, content.Days.Count);
        var queuedIds = content.Days.SelectMany(day => day.WordIds.Concat(day.ExposureWordIds)).ToList();
        var queuedWords = await db.Words.AsNoTracking()
            .Include(word => word.Scenarios)
            .Where(word => queuedIds.Contains(word.Id))
            .ToListAsync();
        var byId = queuedWords.ToDictionary(word => word.Id);
        foreach (var day in content.Days)
        {
            Assert.True(day.ExposureWordIds.Count <= 2, $"接触词超限量：{day.ExposureWordIds.Count}");
            Assert.All(day.ExposureWordIds, id => Assert.True(byId[id].CefrLevel > UserBand, "接触词必须超带"));
            Assert.All(day.WordIds.Concat(day.ExposureWordIds), id =>
                Assert.True(byId[id].Scenarios.Count == 0
                    || byId[id].Scenarios.Any(item => content.FocusScenarios.Contains(item.ScenarioKey.ToLowerInvariant())),
                    "词队列只取主攻场景或 core 桶"));
        }

        // 同日重复触发幂等
        var again = await service.GenerateAsync(user.Id, CancellationToken.None);
        Assert.Equal(plan.Id, again.Id);
        Assert.Equal(1, await db.LearningPlans.CountAsync(item => item.UserId == user.Id));
    }

    [Fact]
    public async Task Generate_falls_back_to_scenario_coverage_when_no_profile()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserWithBandAsync(db, "plan-coverage");
        var service = CreatePlanService(db);

        var plan = await service.GenerateAsync(user.Id, CancellationToken.None);

        var content = JsonSerializer.Deserialize<LearningPlanContent>(plan.ContentJson, JsonOptions)!;
        Assert.InRange(content.FocusScenarios.Count, 1, 2);
        Assert.All(content.FocusScenarios, key => Assert.True(ScenarioTaxonomy.IsSubScenarioKey(key)));
        Assert.Empty(content.SourceFindingIds);
    }

    [Fact]
    public async Task Daily_words_execute_plan_with_capped_exposure()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserWithBandAsync(db, "plan-daily");
        await SeedWordPoolAsync(db, "daily");
        await SeedProfileAsync(db, user.Id);
        var planService = CreatePlanService(db);
        await planService.GenerateAsync(user.Id, CancellationToken.None);

        var daily = new DailyWordSelectionService(db, CreateScoreProfile(db), planService);
        var items = await daily.GetDailyAsync(user.Id, 10, CancellationToken.None);

        Assert.NotEmpty(items);
        Assert.All(items, item => Assert.True(item.FromPlan, "有当日 Plan 时每日词应来自计划"));
        var exposure = items.Where(item => item.IsExposure).ToList();
        Assert.True(exposure.Count <= 2, $"接触词占比应 ≤20%，实际 {exposure.Count}/10");
        // 接触词全部超带
        var exposureIds = exposure.Select(item => item.Id).ToList();
        var words = await db.Words.AsNoTracking().Where(word => exposureIds.Contains(word.Id)).ToListAsync();
        Assert.All(words, word => Assert.True(word.CefrLevel > UserBand));
    }

    [Fact]
    public async Task Daily_words_fall_back_when_plan_expired_or_missing()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var planService = CreatePlanService(db);
        var daily = new DailyWordSelectionService(db, CreateScoreProfile(db), planService);

        // 过期（>7 天）计划：回退难度带
        var expiredUser = await SeedUserWithBandAsync(db, "plan-expired");
        await SeedWordPoolAsync(db, "expired");
        var content = new LearningPlanContent(["dining_out"], [], [],
            Enumerable.Range(0, 7).Select(_ => new LearningPlanDay([], [], [])).ToList());
        db.LearningPlans.Add(new LearningPlan
        {
            UserId = expiredUser.Id,
            StartDate = LearningPlanService.Today().AddDays(-8),
            ContentJson = JsonSerializer.Serialize(content, JsonOptions)
        });
        await db.SaveChangesAsync();

        Assert.Null(await planService.GetActiveAsync(expiredUser.Id, CancellationToken.None));
        var expiredItems = await daily.GetDailyAsync(expiredUser.Id, 10, CancellationToken.None);
        Assert.NotEmpty(expiredItems);
        Assert.All(expiredItems, item => Assert.False(item.FromPlan, "过期 Plan 应回退难度带选词"));

        // 无计划用户：回退不受影响
        var plainUser = await SeedUserWithBandAsync(db, "plan-missing");
        var plainItems = await daily.GetDailyAsync(plainUser.Id, 10, CancellationToken.None);
        Assert.NotEmpty(plainItems);
        Assert.All(plainItems, item => Assert.False(item.FromPlan));
    }

    [Fact]
    public async Task Sentence_prompts_use_plan_targets()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserWithBandAsync(db, "plan-sentence");
        await SeedWordPoolAsync(db, "sentence");
        await SeedProfileAsync(db, user.Id);
        var planService = CreatePlanService(db);
        var plan = await planService.GenerateAsync(user.Id, CancellationToken.None);
        var content = JsonSerializer.Deserialize<LearningPlanContent>(plan.ContentJson, JsonOptions)!;
        var todayTargets = content.Days[0].SentenceTargets;
        Assert.Equal(3, todayTargets.Count);

        var sentences = new SentenceService(
            db,
            new ThrowingLlmFactory(),
            Options.Create(new LlmSentenceRatingOptions()),
            planService,
            CreateScoreProfile(db));
        var batch = await sentences.GetPersonalizedPromptsAsync(user.Id, 3, CancellationToken.None);

        Assert.True(batch.FromPlan);
        Assert.Equal(3, batch.Prompts.Count);
        // 造句目标全部带内（CefrLevel == 用户水平带 A2）
        Assert.All(batch.Prompts, prompt => Assert.Contains(prompt.TargetWord, todayTargets));
    }

    [Fact]
    public async Task Recommended_articles_prefer_focus_scenario_and_fall_back_without_plan()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserWithBandAsync(db, "plan-reading");
        await SeedWordPoolAsync(db, "reading");
        await SeedProfileAsync(db, user.Id);
        var matched = new Article
        {
            Title = $"Dining article {Guid.NewGuid():N}",
            Content = "Ordering food in a restaurant.",
            CefrLevel = CefrLevel.A2,
            DifficultyLevel = DifficultyLevel.Basic,
            TopicTag = "dining_out",
            WordCount = 20
        };
        var other = new Article
        {
            Title = $"Space article {Guid.NewGuid():N}",
            Content = "Stars and planets far away.",
            CefrLevel = CefrLevel.A2,
            DifficultyLevel = DifficultyLevel.Basic,
            TopicTag = "science",
            WordCount = 20
        };
        db.Articles.AddRange(matched, other);
        await db.SaveChangesAsync();

        var planService = CreatePlanService(db);
        await planService.GenerateAsync(user.Id, CancellationToken.None);
        var articles = new ArticleService(db, planService, CreateScoreProfile(db));

        var recommended = await articles.GetRecommendedAsync(user.Id, CancellationToken.None);
        Assert.True(recommended.FromPlan);
        Assert.Equal(matched.Id, recommended.Articles[0].Id);

        // 无 Plan 用户：难度就近回退，不标记来自计划
        var plainUser = await SeedUserWithBandAsync(db, "plan-reading-plain");
        var fallback = await articles.GetRecommendedAsync(plainUser.Id, CancellationToken.None);
        Assert.False(fallback.FromPlan);
        Assert.NotEmpty(fallback.Articles);
    }

    // ── 数据播种 ─────────────────────────────────────────────

    private static async Task<User> SeedUserWithBandAsync(ApplicationDbContext db, string name)
    {
        var user = new User { DisplayName = $"{name}-{Guid.NewGuid():N}" };
        db.Users.Add(user);
        // 水平带 A2（CefrDisplay 驱动 Plan/造句带内约束；VocabularyScore 驱动每日词回退路径）
        db.UserProgress.Add(new UserProgress { UserId = user.Id, VocabularyScore = 50, CefrDisplay = "A2" });
        await db.SaveChangesAsync();
        return user;
    }

    /// <summary>带内主攻场景词 12 个（A2）+ 超带接触词 4 个（B1）+ core 桶带内词 3 个（A2）。
    /// Utility 置空：Planner 词池过滤是 Utility != Low（含未标注），而测评词池要求 High/Medium——避免共享库中污染测评测试的词池。</summary>
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
                // 已标记为当前标注版本，避免共享库中 ScenarioAnnotationWorker 测试扫入待标队列
                ScenarioAnnotationVersion = ScenarioAnnotationWorker.CurrentVersion
            };
            word.Scenarios.Add(new WordScenario { WordId = word.Id, ScenarioKey = "dining_out" });
            db.Words.Add(word);
        }

        for (var i = 0; i < 4; i++)
        {
            var word = new Word
            {
                Lemma = $"exposure{suffix}{i}",
                Meanings = ["接触词"],
                CefrLevel = CefrLevel.B1,
                DifficultyLevel = DifficultyLevel.Advanced,
                ScenarioAnnotationVersion = ScenarioAnnotationWorker.CurrentVersion
            };
            word.Scenarios.Add(new WordScenario { WordId = word.Id, ScenarioKey = "dining_out" });
            db.Words.Add(word);
        }

        for (var i = 0; i < 3; i++)
        {
            db.Words.Add(new Word
            {
                Lemma = $"core{suffix}{i}",
                Meanings = ["通用"],
                CefrLevel = CefrLevel.A2,
                DifficultyLevel = DifficultyLevel.Intermediate,
                ScenarioAnnotationVersion = ScenarioAnnotationWorker.CurrentVersion
            });
        }

        await db.SaveChangesAsync();
    }

    /// <summary>画像：Verified 场景 weakness（dining_out）+ 存疑场景 weakness + Verified 技能 weakness。返回三者 Id。</summary>
    private static async Task<(long VerifiedId, long QuestionedId, long SkillId)> SeedProfileAsync(ApplicationDbContext db, Guid userId)
    {
        var profile = new WeaknessProfile { UserId = userId, ModelProfileId = "test" };
        var verified = new ProfileFinding
        {
            Dimension = FindingDimension.Scenario,
            DimensionKey = "dining_out",
            Polarity = FindingPolarity.Weakness,
            Statement = "点餐场景词掌握弱。",
            EvidenceJson = "[]",
            Confidence = FindingConfidence.Low,
            Verification = FindingVerification.Verified
        };
        var questioned = new ProfileFinding
        {
            Dimension = FindingDimension.Scenario,
            DimensionKey = "travel_lodging",
            Polarity = FindingPolarity.Weakness,
            Statement = "旅行住宿场景掌握弱（存疑）。",
            EvidenceJson = "[]",
            Confidence = FindingConfidence.Low,
            Verification = FindingVerification.Questioned
        };
        var skill = new ProfileFinding
        {
            Dimension = FindingDimension.Skill,
            DimensionKey = "grammar",
            Polarity = FindingPolarity.Weakness,
            Statement = "语法不稳定。",
            EvidenceJson = "[]",
            Confidence = FindingConfidence.Low,
            Verification = FindingVerification.Verified
        };
        profile.Findings.AddRange(verified, questioned, skill);
        db.WeaknessProfiles.Add(profile);
        await db.SaveChangesAsync();
        return (verified.Id, questioned.Id, skill.Id);
    }

    // ── 服务装配 ─────────────────────────────────────────────

    private static LearningPlanService CreatePlanService(ApplicationDbContext db)
        => new(db, CreateScoreProfile(db));

    private static ScoreProfileService CreateScoreProfile(ApplicationDbContext db)
        => new(db, new ScoreMappingService(new ScoreMappingOptions()));

    /// <summary>造句出题不触发 LLM；误用即失败。</summary>
    private sealed class ThrowingLlmFactory : IUserLlmProviderFactory
    {
        public Task<ILLMProvider> GetForUserAsync(Guid userId, CancellationToken cancellationToken)
            => throw new InvalidOperationException("LLM should not be called in prompt selection.");
    }
}
