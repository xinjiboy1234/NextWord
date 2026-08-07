using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Models;
using NextWord.Domain.Services;
using NextWord.Infrastructure.Data;
using NextWord.Infrastructure.Services;

namespace NextWord.UnitTests;

/// <summary>
/// T-036「我的这个月」聚合（真实 PG）：四类里程碑事件出现与时间过滤、画像 diff 规则
/// （新增强项/好转弱点/单份画像摘要）、最近 3 条洞察回放、固定查询数（无 N+1）。
/// </summary>
public class MonthlyTimelineTests
{
    [Fact]
    public async Task Aggregates_all_event_types_and_filters_by_window()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, "timeline-happy");
        var now = DateTimeOffset.UtcNow;

        // 词毕业：窗内 1 个 + 窗外 1 个（60 天前）
        await SeedGraduatedWordAsync(db, user.Id, "harvest", now.AddDays(-3));
        await SeedGraduatedWordAsync(db, user.Id, "ancient", now.AddDays(-60));

        // 挑战首过：A2 首过在窗内；B1 首过在窗外（窗内再次通过不算首过）
        db.ChallengeRecords.AddRange(
            new ChallengeRecord { UserId = user.Id, Passed = true, AttemptedLevel = CefrLevel.A2, Timestamp = now.AddDays(-5) },
            new ChallengeRecord { UserId = user.Id, Passed = true, AttemptedLevel = CefrLevel.B1, Timestamp = now.AddDays(-40) },
            new ChallengeRecord { UserId = user.Id, Passed = true, AttemptedLevel = CefrLevel.B1, Timestamp = now.AddDays(-2) },
            new ChallengeRecord { UserId = user.Id, Passed = false, AttemptedLevel = CefrLevel.C1, Timestamp = now.AddDays(-1) });

        // 定级升级：窗内 1 条 + 窗外 1 条
        db.LevelHistories.AddRange(
            new LevelHistory { UserId = user.Id, FromLevel = CefrLevel.A2, ToLevel = CefrLevel.B1, Reason = LevelChangeReason.Upgrade, Timestamp = now.AddDays(-10) },
            new LevelHistory { UserId = user.Id, FromLevel = CefrLevel.A1, ToLevel = CefrLevel.A2, Reason = LevelChangeReason.Initial, Timestamp = now.AddDays(-90) });

        // 画像：两份均在窗内（生成事件 ×2，且可 diff）
        var previous = SeedProfile(user.Id, now.AddDays(-20),
            new ProfileFinding { Dimension = FindingDimension.Skill, DimensionKey = "grammar", Polarity = FindingPolarity.Weakness, Statement = "语法错误偏多。", Confidence = FindingConfidence.Medium },
            new ProfileFinding { Dimension = FindingDimension.Scenario, DimensionKey = "dining_out", Polarity = FindingPolarity.Strength, Statement = "点餐场景稳定。", Confidence = FindingConfidence.High });
        var current = SeedProfile(user.Id, now.AddDays(-1),
            new ProfileFinding { Dimension = FindingDimension.Skill, DimensionKey = "grammar", Polarity = FindingPolarity.Strength, Statement = "语法已转稳。", Confidence = FindingConfidence.High },
            new ProfileFinding { Dimension = FindingDimension.Reading, DimensionKey = "reading", Polarity = FindingPolarity.Strength, Statement = "阅读查词减少。", Confidence = FindingConfidence.Medium },
            new ProfileFinding { Dimension = FindingDimension.Scenario, DimensionKey = "travel", Polarity = FindingPolarity.Weakness, Statement = "存疑条目不参与对比。", Confidence = FindingConfidence.Low, Verification = FindingVerification.Questioned });
        db.WeaknessProfiles.AddRange(previous, current);

        // 洞察：4 条 → 回放取最近 3 条
        for (var i = 0; i < 4; i++)
        {
            db.BottleneckInsights.Add(new BottleneckInsight
            {
                UserId = user.Id,
                Nature = BottleneckNature.GrammarErrors,
                Statement = $"结论 {i}。",
                CreatedAt = now.AddDays(-i - 1)
            });
        }
        await db.SaveChangesAsync();

        var result = await new MonthlyTimelineService(db).GetAsync(user.Id, 30, CancellationToken.None);

        // 里程碑：四类齐；窗外事件不出现
        var graduations = result.Events.Where(item => item.Type == MonthlyTimelineEventTypes.WordGraduation).ToList();
        Assert.Single(graduations);
        Assert.StartsWith("harvest", graduations[0].Word);

        var firstPass = Assert.Single(result.Events, item => item.Type == MonthlyTimelineEventTypes.ChallengeFirstPass);
        Assert.Equal("A2", firstPass.Level);

        var levelChange = Assert.Single(result.Events, item => item.Type == MonthlyTimelineEventTypes.LevelChange);
        Assert.Equal("A2", levelChange.FromLevel);
        Assert.Equal("B1", levelChange.ToLevel);
        Assert.Equal("Upgrade", levelChange.Reason);

        Assert.Equal(2, result.Events.Count(item => item.Type == MonthlyTimelineEventTypes.ProfileGenerated));
        // 倒序排列
        Assert.True(result.Events.SequenceEqual(result.Events.OrderByDescending(item => item.OccurredAt)));

        // 画像变化：grammar 弱转强 → 既是新增强项也是好转弱点；reading 纯新增强项；存疑条目不出现
        Assert.True(result.ProfileChange.HasProfile);
        Assert.True(result.ProfileChange.HasComparison);
        Assert.Equal(2, result.ProfileChange.NewStrengths.Count);
        Assert.Contains(result.ProfileChange.NewStrengths, item => item.DimensionKey == "grammar" && item.Statement == "语法已转稳。");
        Assert.Contains(result.ProfileChange.NewStrengths, item => item.DimensionKey == "reading");
        var improved = Assert.Single(result.ProfileChange.ImprovedWeaknesses);
        Assert.Equal("grammar", improved.DimensionKey);
        Assert.Equal("语法已转稳。", improved.Statement); // 当前同维度位有结论则用当前的

        // 洞察回放：最近 3 条、倒序
        Assert.Equal(3, result.Insights.Count);
        Assert.Equal("结论 0。", result.Insights[0].Statement);
        Assert.Equal("GrammarErrors", result.Insights[0].Nature);
    }

    [Fact]
    public async Task Single_profile_yields_summary_without_comparison()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, "timeline-single");
        var profile = SeedProfile(user.Id, DateTimeOffset.UtcNow.AddDays(-2),
            new ProfileFinding { Dimension = FindingDimension.Skill, DimensionKey = "grammar", Polarity = FindingPolarity.Strength, Statement = "语法稳定。", Confidence = FindingConfidence.High });
        db.WeaknessProfiles.Add(profile);
        await db.SaveChangesAsync();

        var result = await new MonthlyTimelineService(db).GetAsync(user.Id, 30, CancellationToken.None);

        Assert.True(result.ProfileChange.HasProfile);
        Assert.False(result.ProfileChange.HasComparison);
        Assert.Empty(result.ProfileChange.NewStrengths);
        Assert.Empty(result.ProfileChange.ImprovedWeaknesses);
        var summary = Assert.Single(result.ProfileChange.CurrentFindings);
        Assert.Equal("语法稳定。", summary.Statement);
        Assert.Equal(FindingPolarity.Strength, summary.Polarity);
    }

    [Fact]
    public async Task Empty_user_gets_empty_sections()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, "timeline-empty");

        var result = await new MonthlyTimelineService(db).GetAsync(user.Id, 30, CancellationToken.None);

        Assert.Empty(result.Events);
        Assert.False(result.ProfileChange.HasProfile);
        Assert.False(result.ProfileChange.HasComparison);
        Assert.Empty(result.Insights);
    }

    [Fact]
    public async Task Query_count_stays_bounded_regardless_of_event_volume()
    {
        // 先触发一次库初始化（计数上下文与单测库同连接串）
        await using var warmup = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(warmup, "timeline-nplus1");
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 20; i++)
        {
            await SeedGraduatedWordAsync(warmup, user.Id, $"word{i}", now.AddDays(-1));
            warmup.ChallengeRecords.Add(new ChallengeRecord { UserId = user.Id, Passed = true, AttemptedLevel = CefrLevel.A1 + (i % 3), Timestamp = now.AddDays(-i - 1) });
            warmup.LevelHistories.Add(new LevelHistory { UserId = user.Id, FromLevel = CefrLevel.A1, ToLevel = CefrLevel.A2, Reason = LevelChangeReason.Upgrade, Timestamp = now.AddDays(-i - 1) });
        }
        var p1 = SeedProfile(user.Id, now.AddDays(-10),
            new ProfileFinding { Dimension = FindingDimension.Skill, DimensionKey = "grammar", Polarity = FindingPolarity.Weakness, Statement = "旧弱点。", Confidence = FindingConfidence.Low });
        var p2 = SeedProfile(user.Id, now.AddDays(-1),
            new ProfileFinding { Dimension = FindingDimension.Skill, DimensionKey = "grammar", Polarity = FindingPolarity.Strength, Statement = "新强项。", Confidence = FindingConfidence.High });
        warmup.WeaknessProfiles.AddRange(p1, p2);
        await warmup.SaveChangesAsync();

        var counter = new CommandCounter();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(PostgresTestDatabase.ConnectionString)
            .AddInterceptors(counter)
            .Options;
        await using var db = new ApplicationDbContext(options);

        var result = await new MonthlyTimelineService(db).GetAsync(user.Id, 30, CancellationToken.None);

        Assert.Equal(20, result.Events.Count(item => item.Type == MonthlyTimelineEventTypes.WordGraduation));
        // 固定查询数：毕业/挑战/等级/画像事件/最新画像(含 Finding)/洞察 ≤ 8 次往返，与事件量无关
        Assert.True(counter.Count <= 8, $"查询数 {counter.Count} 超出上限，疑似 N+1");
    }

    // ── 画像 diff 纯规则 ─────────────────────────────────────

    [Fact]
    public void Diff_flags_new_strength_and_improved_weakness()
    {
        var previous = new[]
        {
            Finding(FindingDimension.Skill, "grammar", FindingPolarity.Weakness, "语法薄弱。"),
            Finding(FindingDimension.Scenario, "dining_out", FindingPolarity.Strength, "点餐稳定。"),
            Finding(FindingDimension.Reading, "reading", FindingPolarity.Weakness, "阅读查词多。")
        };
        var current = new[]
        {
            Finding(FindingDimension.Skill, "grammar", FindingPolarity.Strength, "语法转稳。"),
            Finding(FindingDimension.Scenario, "dining_out", FindingPolarity.Strength, "点餐依旧稳定。"),
            Finding(FindingDimension.Skill, "natural", FindingPolarity.Strength, "表达更自然。")
        };

        var diff = ProfileChangeDiffer.Diff(previous, current);

        // 新增强项：grammar（弱转强）+ natural（新增）；dining_out 上一份已是强项不算
        Assert.Equal(2, diff.NewStrengths.Count);
        Assert.DoesNotContain(diff.NewStrengths, item => item.DimensionKey == "dining_out");
        // 好转弱点：grammar（转强项，用当前结论）+ reading（不再上榜，沿用旧结论）
        Assert.Equal(2, diff.ImprovedWeaknesses.Count);
        Assert.Contains(diff.ImprovedWeaknesses, item => item.DimensionKey == "grammar" && item.Statement == "语法转稳。");
        Assert.Contains(diff.ImprovedWeaknesses, item => item.DimensionKey == "reading" && item.Statement == "阅读查词多。");
    }

    [Fact]
    public void Diff_ignores_questioned_findings()
    {
        var previous = new[] { Finding(FindingDimension.Skill, "grammar", FindingPolarity.Weakness, "语法薄弱。") };
        var current = new[]
        {
            Finding(FindingDimension.Skill, "grammar", FindingPolarity.Strength, "存疑的转强。", verification: FindingVerification.Questioned)
        };

        var diff = ProfileChangeDiffer.Diff(previous, current);

        Assert.Empty(diff.NewStrengths);
        // 当前存疑条目不参与 → grammar 视为「不再是弱点」，沿用上一份结论
        var improved = Assert.Single(diff.ImprovedWeaknesses);
        Assert.Equal("语法薄弱。", improved.Statement);
    }

    // ── 数据播种 ─────────────────────────────────────────────

    private static async Task<User> SeedUserAsync(ApplicationDbContext db, string name)
    {
        var user = new User { DisplayName = $"{name}-{Guid.NewGuid():N}" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<Word> SeedGraduatedWordAsync(ApplicationDbContext db, Guid userId, string lemma, DateTimeOffset graduatedAt)
    {
        var word = new Word
        {
            Lemma = $"{lemma}-{Guid.NewGuid():N}"[..Math.Min(lemma.Length + 9, 32)],
            Meanings = ["测试词"],
            CefrLevel = CefrLevel.A2,
            DifficultyLevel = DifficultyLevel.Basic
        };
        db.Words.Add(word);
        db.UserWordRelationships.Add(new UserWordRelationship
        {
            UserId = userId,
            WordId = word.Id,
            LifecycleStage = WordLifecycleStage.SpontaneousUse,
            StageUpdatedAt = graduatedAt,
            GraduatedFreeExpressionLogId = Guid.NewGuid()
        });
        await db.SaveChangesAsync();
        return word;
    }

    private static WeaknessProfile SeedProfile(Guid userId, DateTimeOffset createdAt, params ProfileFinding[] findings)
        => new()
        {
            UserId = userId,
            CreatedAt = createdAt,
            Findings = findings.ToList()
        };

    private static ProfileFinding Finding(
        FindingDimension dimension,
        string dimensionKey,
        FindingPolarity polarity,
        string statement,
        FindingVerification verification = FindingVerification.Verified)
        => new()
        {
            Dimension = dimension,
            DimensionKey = dimensionKey,
            Polarity = polarity,
            Statement = statement,
            Confidence = FindingConfidence.Medium,
            Verification = verification
        };

    /// <summary>统计 EF Core 实际执行的查询命令数（N+1 断言用）。</summary>
    private sealed class CommandCounter : DbCommandInterceptor
    {
        public int Count;

        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Count);
            return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
        }
    }
}
