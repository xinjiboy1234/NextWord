namespace NextWord.Domain.Enums;

/// <summary>瓶颈性质分类（T-007，DESIGN-bottleneck-insight §2.2）：InsightAgent 细读产出原文后的判断。</summary>
public enum BottleneckNature
{
    /// <summary>词汇量不足。</summary>
    VocabularyInsufficient = 1,
    /// <summary>会词但组织不成句。</summary>
    CannotOrganizeSentences = 2,
    /// <summary>语法错误多。</summary>
    GrammarErrors = 3,
    /// <summary>语法正确但表达单调。</summary>
    MonotonousExpression = 4,
    /// <summary>回避模式（能力范围收缩）。</summary>
    AvoidancePattern = 5,
    /// <summary>中式搭配。</summary>
    ChinglishCollocation = 6,
    /// <summary>安全词策略（新学内容从不进入产出）。</summary>
    SafeWordStrategy = 7
}

/// <summary>指标筛查信号（T-007 §2.1）：规则只判「要不要细看」，不做结论。</summary>
public enum BottleneckSignal
{
    /// <summary>平台期：近 10 次产出四维均分无显著提升且持续活跃。</summary>
    Plateau = 1,
    /// <summary>回避模式：从句/复杂连接使用率持续走低。</summary>
    Avoidance = 2,
    /// <summary>安全词策略：Plan 造句目标词在自由产出中出现率为 0。</summary>
    SafeWord = 3
}

/// <summary>信号的线上表示（任务 payload / 持久化，小写蛇形）。</summary>
public static class BottleneckSignalNames
{
    public static string ToWireName(this BottleneckSignal signal) => signal switch
    {
        BottleneckSignal.Plateau => "plateau",
        BottleneckSignal.Avoidance => "avoidance",
        BottleneckSignal.SafeWord => "safe_word",
        _ => throw new ArgumentOutOfRangeException(nameof(signal), signal, null)
    };

    public static bool TryParse(string value, out BottleneckSignal signal)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "plateau": signal = BottleneckSignal.Plateau; return true;
            case "avoidance": signal = BottleneckSignal.Avoidance; return true;
            case "safe_word": signal = BottleneckSignal.SafeWord; return true;
            default: signal = default; return false;
        }
    }
}
