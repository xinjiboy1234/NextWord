using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Infrastructure.Data;
using System.Text.Json;

namespace NextWord.Infrastructure.Services;

/// <summary>
/// 指标筛查（T-007，DESIGN-bottleneck-insight §2.1）：纯规则、零 LLM，随日快照任务运行。
/// 三类信号满足其一即触发 InsightAgent；规则只判「要不要细看」，不做结论。
/// 平台期：近 10 次产出四维均分斜率≈0 且波动小、窗口内有持续活跃；
/// 回避模式：复杂连接使用率后半段相比前半段腰斩（能力范围收缩）；
/// 安全词策略：生效 Plan 的造句目标词在近期自由产出中出现率为 0。
/// </summary>
public sealed class BottleneckScreeningService(ApplicationDbContext db) : IBottleneckScreeningService
{
    public const int PlateauWindow = 10;
    /// <summary>四维均分（0-5）斜率绝对值上限（分/次），低于视为无显著提升。</summary>
    public const double PlateauMaxSlope = 0.05;
    /// <summary>四维均分波动（标准差）上限。</summary>
    public const double PlateauMaxStdDev = 0.5;
    /// <summary>持续活跃：窗口首末跨度上限（天），10 次产出拖太久不算持续活跃。</summary>
    public const int PlateauMaxSpanDays = 30;
    /// <summary>回避模式判定最少样本数。</summary>
    public const int AvoidanceMinSamples = 10;
    public const int AvoidanceWindow = 12;
    /// <summary>前半段曾经常用复杂连接（每句平均数下限）才谈得上「走低」。</summary>
    public const double AvoidanceMinBaseRate = 0.3;
    /// <summary>后半段连接使用率 ≤ 前半段 × 该比例视为持续走低。</summary>
    public const double AvoidanceDropRatio = 0.5;
    /// <summary>安全词策略判定所需的最少自由产出样本数（自 Plan 创建起）。</summary>
    public const int SafeWordMinFreeSamples = 3;
    /// <summary>新 Plan 宽限期：创建未满 24h 不做安全词判定（窗口内样本必然过少，易误判，T-012）。</summary>
    public static readonly TimeSpan SafeWordGracePeriod = TimeSpan.FromHours(24);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>从句/复杂连接词表（规则近似，只覆盖单词连接词，多词短语留给 InsightAgent 细读）。</summary>
    private static readonly HashSet<string> ComplexConnectives = new(StringComparer.OrdinalIgnoreCase)
    {
        "because", "although", "though", "which", "that", "when", "while", "if", "unless",
        "however", "therefore", "since", "after", "before", "who", "whom", "whose", "where",
        "until", "whenever", "whereas", "moreover", "furthermore", "besides", "thus", "hence",
        "otherwise", "meanwhile", "nevertheless", "despite", "whether"
    };

    public async Task<IReadOnlyList<BottleneckSignal>> ScreenAsync(Guid userId, CancellationToken cancellationToken)
    {
        var logs = (await db.SentenceLogs.AsNoTracking()
                .Where(log => log.UserId == userId)
                .OrderByDescending(log => log.Timestamp)
                .Take(Math.Max(PlateauWindow, AvoidanceWindow))
                .ToListAsync(cancellationToken))
            .OrderBy(log => log.Timestamp)
            .ToList();

        var signals = new List<BottleneckSignal>();
        if (IsPlateau(logs))
        {
            signals.Add(BottleneckSignal.Plateau);
        }

        if (IsAvoidance(logs))
        {
            signals.Add(BottleneckSignal.Avoidance);
        }

        if (await IsSafeWordStrategyAsync(userId, cancellationToken))
        {
            signals.Add(BottleneckSignal.SafeWord);
        }

        return signals;
    }

    /// <summary>平台期：近 10 次产出四维均分斜率≈0 且波动小于阈值，且窗口跨度内持续活跃。</summary>
    internal static bool IsPlateau(IReadOnlyList<SentenceLog> chronologicalLogs)
    {
        if (chronologicalLogs.Count < PlateauWindow)
        {
            return false;
        }

        var window = chronologicalLogs.TakeLast(PlateauWindow).ToList();
        var averages = window
            .Select(log => (log.GrammarScore + log.NaturalScore + log.VocabularyScore + log.RelevanceScore) / 4.0)
            .ToList();
        var spanDays = (window[^1].Timestamp - window[0].Timestamp).TotalDays;
        if (spanDays > PlateauMaxSpanDays)
        {
            return false;
        }

        return Math.Abs(Slope(averages)) <= PlateauMaxSlope && StdDev(averages) <= PlateauMaxStdDev;
    }

    /// <summary>回避模式：复杂连接使用率后半段相比前半段腰斩，且前半段曾经常使用。</summary>
    internal static bool IsAvoidance(IReadOnlyList<SentenceLog> chronologicalLogs)
    {
        if (chronologicalLogs.Count < AvoidanceMinSamples)
        {
            return false;
        }

        var window = chronologicalLogs.TakeLast(AvoidanceWindow).ToList();
        var half = window.Count / 2;
        var firstRate = ConnectiveRate(window.Take(half));
        var secondRate = ConnectiveRate(window.Skip(half));
        return firstRate >= AvoidanceMinBaseRate && secondRate <= firstRate * AvoidanceDropRatio;
    }

    /// <summary>
    /// 安全词策略：生效 Plan 的造句目标词在近期自由产出（≥3 篇）中出现率为 0。
    /// T-012 修复口径：产出窗口从 Plan.CreatedAt 起算（不是生效日 00:00——那会错误计入 Plan 创建前的产出），
    /// 且新 Plan 有 24h 宽限期（创建未满 24h 不判定，窗口内样本必然过少易误判）；双重保险。
    /// </summary>
    private async Task<bool> IsSafeWordStrategyAsync(Guid userId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        var plan = await db.LearningPlans.AsNoTracking()
            .Where(item => item.UserId == userId && item.StartDate <= today && item.StartDate >= today.AddDays(-(LearningPlanService.PlanDays - 1)))
            .OrderByDescending(item => item.StartDate)
            .FirstOrDefaultAsync(cancellationToken);
        if (plan is null)
        {
            return false;
        }

        // 宽限期：新 Plan 创建未满 24h 不做安全词判定
        if (plan.CreatedAt > DateTimeOffset.UtcNow - SafeWordGracePeriod)
        {
            return false;
        }

        var content = JsonSerializer.Deserialize<LearningPlanContent>(plan.ContentJson, JsonOptions);
        var targets = content?.Days
            .SelectMany(day => day.SentenceTargets)
            .Select(target => target.Trim().ToLowerInvariant())
            .Where(target => target.Length > 0)
            .Distinct()
            .ToList() ?? [];
        if (targets.Count == 0)
        {
            return false;
        }

        // 窗口从 Plan 创建时刻起算：Plan 创建前的产出与新目标词无关，不计入判定
        var freeTexts = await db.FreeExpressionLogs.AsNoTracking()
            .Where(log => log.UserId == userId && log.Timestamp >= plan.CreatedAt)
            .OrderByDescending(log => log.Timestamp)
            .Take(20)
            .Select(log => log.UserText)
            .ToListAsync(cancellationToken);
        if (freeTexts.Count < SafeWordMinFreeSamples)
        {
            return false;
        }

        return freeTexts.All(text => !Tokenize(text).Overlaps(targets));
    }

    /// <summary>复杂连接使用率：每句平均不同连接词数（分词去重，近似口径足够筛查用）。</summary>
    private static double ConnectiveRate(IEnumerable<SentenceLog> logs)
    {
        var total = 0;
        var count = 0;
        foreach (var log in logs)
        {
            total += Tokenize(log.UserSentence).Count(ComplexConnectives.Contains);
            count += 1;
        }

        return count == 0 ? 0 : (double)total / count;
    }

    /// <summary>分词：小写字母序列（词边界匹配，避免子串误判）。</summary>
    internal static HashSet<string> Tokenize(string text)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = new System.Text.StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsLetter(ch))
            {
                current.Append(char.ToLowerInvariant(ch));
            }
            else if (current.Length > 0)
            {
                tokens.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }

    /// <summary>最小二乘斜率（自变量为序号 0..n-1）。</summary>
    private static double Slope(IReadOnlyList<double> values)
    {
        var n = values.Count;
        var meanX = (n - 1) / 2.0;
        var meanY = values.Average();
        var numerator = 0.0;
        var denominator = 0.0;
        for (var i = 0; i < n; i++)
        {
            numerator += (i - meanX) * (values[i] - meanY);
            denominator += (i - meanX) * (i - meanX);
        }

        return denominator == 0 ? 0 : numerator / denominator;
    }

    private static double StdDev(IReadOnlyList<double> values)
    {
        var mean = values.Average();
        return Math.Sqrt(values.Sum(value => (value - mean) * (value - mean)) / values.Count);
    }
}
