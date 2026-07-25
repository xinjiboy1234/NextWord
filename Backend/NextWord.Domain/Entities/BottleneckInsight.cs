using NextWord.Domain.Enums;

namespace NextWord.Domain.Entities;

/// <summary>
/// 瓶颈性质洞察（T-007，DESIGN-bottleneck-insight §2.2）：指标筛查触发后由 InsightAgent
/// 细读产出原文产出。Signals 记录触发信号；EvidenceJson 为 SentenceLog id 列表（沿用画像证据纪律）；
/// ReplanTriggered = 瓶颈性质相比上一条洞察已变化（首次发现也算变化），已触发重规划。
/// 洞察只影响解读与规划，不改任何分数。
/// </summary>
public sealed class BottleneckInsight
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public BottleneckNature Nature { get; set; }
    /// <summary>触发信号（线上名逗号分隔，如 "plateau,avoidance"）。</summary>
    public string Signals { get; set; } = string.Empty;
    /// <summary>一句中文结论，点名具体行为。</summary>
    public string Statement { get; set; } = string.Empty;
    /// <summary>证据引用的 SentenceLog id 列表（JSON）。</summary>
    public string EvidenceJson { get; set; } = "[]";
    /// <summary>性质已变 → 已触发重规划（重生成画像 + 强制 Planner）；性质未变 → 仅记录。</summary>
    public bool ReplanTriggered { get; set; }
    public string ModelProfileId { get; set; } = "local-dev";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User? User { get; set; }
}
