using NextWord.Domain.Enums;

namespace NextWord.Domain.Models;

/// <summary>月度时间轴事件类型（T-036）：word_graduation / challenge_first_pass / level_change / profile_generated。</summary>
public static class MonthlyTimelineEventTypes
{
    public const string WordGraduation = "word_graduation";
    public const string ChallengeFirstPass = "challenge_first_pass";
    public const string LevelChange = "level_change";
    public const string ProfileGenerated = "profile_generated";
}

/// <summary>月度时间轴上的一条里程碑事件（T-036）。结构化字段由前端本地化为中文文案。</summary>
public sealed record MonthlyTimelineEvent(
    string Type,
    DateTimeOffset OccurredAt,
    string? Word = null,
    string? Level = null,
    string? FromLevel = null,
    string? ToLevel = null,
    string? Reason = null);

/// <summary>画像变化条目（T-036）：diff 出的一条「新增强项」或「好转弱点」。</summary>
public sealed record ProfileChangeItem(FindingDimension Dimension, string DimensionKey, string Statement);

/// <summary>当前画像 Finding 摘要条目（T-036）：只有一份画像时前端展示用。</summary>
public sealed record ProfileFindingSummaryItem(
    FindingDimension Dimension,
    string DimensionKey,
    FindingPolarity Polarity,
    string Statement);

/// <summary>画像变化区块数据（T-036）：HasComparison=false 时看 CurrentFindings 摘要。</summary>
public sealed record MonthlyProfileChange(
    bool HasProfile,
    bool HasComparison,
    DateTimeOffset? CurrentProfileAt,
    IReadOnlyList<ProfileChangeItem> NewStrengths,
    IReadOnlyList<ProfileChangeItem> ImprovedWeaknesses,
    IReadOnlyList<ProfileFindingSummaryItem> CurrentFindings);

/// <summary>洞察回放条目（T-036）：最近瓶颈洞察（性质枚举名 + 结论 + 时间）。</summary>
public sealed record MonthlyInsightItem(string Nature, string Statement, DateTimeOffset CreatedAt);

/// <summary>「我的这个月」聚合结果（T-036）：里程碑事件 + 画像变化 + 洞察回放。</summary>
public sealed record MonthlyTimelineResult(
    int Days,
    IReadOnlyList<MonthlyTimelineEvent> Events,
    MonthlyProfileChange ProfileChange,
    IReadOnlyList<MonthlyInsightItem> Insights);
