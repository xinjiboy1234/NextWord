using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Infrastructure.Data;
using System.Text.Json;

namespace NextWord.Infrastructure.Services;

/// <summary>
/// 指标筛查（T-007，DESIGN-bottleneck-insight §2.1；T-033 v2，DESIGN-insight-signals-v2）：
/// 纯规则、零 LLM，随日快照任务运行。四类信号满足其一即触发 InsightAgent；规则只判「要不要细看」，不做结论。
/// 平台期：近 12 次产出四维均分斜率≈0 且波动小、窗口内有持续活跃；
/// 回避模式：复杂连接使用率后半段 ≤ 前半段 × 0.5 且前半段率 > 0（相对自身基线，不设绝对下限）；
/// 零起步：近 10 次产出复杂连接恒 0 且平均句长无增长（不定性，性质交 InsightAgent 细读）；
/// 安全词策略：生效 Plan 的造句目标词在最近 5 篇自由产出（跨计划周期累计）中出现率为 0。
/// </summary>
public sealed class BottleneckScreeningService(ApplicationDbContext db) : IBottleneckScreeningService
{
    public const int PlateauWindow = 12;
    /// <summary>四维均分（0-5）斜率绝对值上限（分/次），低于视为无显著提升。</summary>
    public const double PlateauMaxSlope = 0.05;
    /// <summary>四维均分波动（标准差）上限（T-033：0.5 → 1.0，菜鸟真实波动 0.8–1.5，放宽后斜率仍是主判据）。</summary>
    public const double PlateauMaxStdDev = 1.0;
    /// <summary>持续活跃：窗口首末跨度上限（天），窗口内产出拖太久不算持续活跃。</summary>
    public const int PlateauMaxSpanDays = 30;
    /// <summary>回避模式判定最少样本数。</summary>
    public const int AvoidanceMinSamples = 10;
    public const int AvoidanceWindow = 12;
    /// <summary>后半段连接使用率 ≤ 前半段 × 该比例视为持续走低（前半段率 > 0 即有基线，T-033 起不设绝对下限）。</summary>
    public const double AvoidanceDropRatio = 0.5;
    /// <summary>零起步信号窗口（次产出）。</summary>
    public const int ColdStartWindow = 10;
    /// <summary>零起步：后半段平均句长 ≤ 前半段 × 该比例视为无增长。</summary>
    public const double ColdStartMaxLengthGrowth = 1.1;
    /// <summary>零起步持续活跃：窗口首末跨度上限（天），同平台期口径。</summary>
    public const int ColdStartMaxSpanDays = 30;
    /// <summary>安全词策略判定样本：最近 N 篇自由产出（T-033：跨计划周期累计，窗口按篇数不按天数）。</summary>
    public const int SafeWordMinFreeSamples = 5;
    /// <summary>新 Plan 宽限期：创建未满 24h 不做安全词判定（新目标词刚下发，未用不算回避，T-012）。</summary>
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

    /// <summary>停用词表（从简，T-033）：安全词短语目标拆词后剔除功能词，只留内容词做匹配。</summary>
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "to", "of", "in", "on", "at", "for", "with", "and", "or",
        "is", "are", "was", "were", "be", "am", "do", "does", "did",
        "i", "you", "he", "she", "it", "we", "they", "my", "your", "his", "her", "our", "their",
        "but", "as", "by", "from", "up", "out", "into"
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

        if (IsColdStart(logs))
        {
            signals.Add(BottleneckSignal.ColdStart);
        }

        if (await IsSafeWordStrategyAsync(userId, cancellationToken))
        {
            signals.Add(BottleneckSignal.SafeWord);
        }

        return signals;
    }

    /// <summary>平台期：近 12 次产出四维均分斜率≈0 且波动小于阈值，且窗口跨度内持续活跃。</summary>
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

    /// <summary>
    /// 回避模式（T-033 相对基线）：后半段复杂连接使用率 ≤ 前半段 × 0.5 且前半段率 > 0。
    /// 曾经用过就算有基线，不设绝对下限；从未用过的用户不判回避——交零起步信号覆盖。
    /// </summary>
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
        return firstRate > 0 && secondRate <= firstRate * AvoidanceDropRatio;
    }

    /// <summary>
    /// 零起步（T-033，DESIGN-insight-signals-v2 §2.3）：近 10 次产出复杂连接恒 0（从没用过）
    /// 且平均句长（词数）无增长（后半段均值 ≤ 前半段 × 1.1），窗口跨度 ≤30 天（持续活跃）。
    /// 信号只表示「产出一直停在简单句」，不定性——性质交 InsightAgent 细读原文。
    /// </summary>
    internal static bool IsColdStart(IReadOnlyList<SentenceLog> chronologicalLogs)
    {
        if (chronologicalLogs.Count < ColdStartWindow)
        {
            return false;
        }

        var window = chronologicalLogs.TakeLast(ColdStartWindow).ToList();
        var spanDays = (window[^1].Timestamp - window[0].Timestamp).TotalDays;
        if (spanDays > ColdStartMaxSpanDays)
        {
            return false;
        }

        // 恒 0：窗口内复杂连接一次都没出现过（连接数非负，窗口率 0 等价于句句为 0）
        if (ConnectiveRate(window) != 0)
        {
            return false;
        }

        var half = window.Count / 2;
        var firstAvgLength = window.Take(half).Average(log => WordCount(log.UserSentence));
        var secondAvgLength = window.Skip(half).Average(log => WordCount(log.UserSentence));
        return secondAvgLength <= firstAvgLength * ColdStartMaxLengthGrowth;
    }

    /// <summary>
    /// 安全词策略（T-033 v2）：生效 Plan 的造句目标词在最近 5 篇自由产出中出现率为 0。
    /// 窗口按篇数不按天数：跨计划周期累计，不再被 7 天计划周期卡死；目标词仍取当前生效 Plan；
    /// 24h 宽限期保留（T-012，新 Plan 创建未满 24h 不判定）。
    /// 多词短语目标匹配口径：拆词后去停用词取内容词，内容词全部在产出中出现（词边界）才算用过——
    /// 避免整串匹配「see eye to eye」永不命中、或拆词后「to」恒命中两个极端。
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
            .Select(target => target.Trim())
            .Where(target => target.Length > 0)
            .Distinct()
            .ToList() ?? [];
        // 目标词 → 内容词集合（去停用词；全是功能词的短语退化为原拆词，避免空集恒命中）
        var targetContentWords = targets
            .Select(TargetContentWords)
            .Where(words => words.Count > 0)
            .ToList();
        if (targetContentWords.Count == 0)
        {
            return false;
        }

        // 窗口：最近 5 篇自由产出（跨计划周期累计）
        var freeTexts = await db.FreeExpressionLogs.AsNoTracking()
            .Where(log => log.UserId == userId)
            .OrderByDescending(log => log.Timestamp)
            .Take(SafeWordMinFreeSamples)
            .Select(log => log.UserText)
            .ToListAsync(cancellationToken);
        if (freeTexts.Count < SafeWordMinFreeSamples)
        {
            return false;
        }

        return freeTexts.All(text =>
        {
            var tokens = Tokenize(text);
            return targetContentWords.All(contentWords => !contentWords.IsSubsetOf(tokens));
        });
    }

    /// <summary>目标词/短语的内容词：拆词去停用词；全是功能词时退化为原拆词（防止空集被 IsSubsetOf 判恒真）。</summary>
    internal static HashSet<string> TargetContentWords(string target)
    {
        var tokens = Tokenize(target);
        var content = tokens
            .Where(token => !StopWords.Contains(token))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return content.Count > 0 ? content : tokens;
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

    /// <summary>词数（Tokenize 同口径：小写字母序列，但不去重），零起步信号用于平均句长。</summary>
    internal static int WordCount(string text)
    {
        var count = 0;
        var inWord = false;
        foreach (var ch in text)
        {
            if (char.IsLetter(ch))
            {
                if (!inWord)
                {
                    count++;
                    inWord = true;
                }
            }
            else
            {
                inWord = false;
            }
        }

        return count;
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
