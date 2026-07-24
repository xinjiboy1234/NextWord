using NextWord.Domain.Enums;

namespace NextWord.Domain.Entities;

/// <summary>
/// 弱点画像（T-005）：一次测评产出一份，由 Profiler Agent 生成、Verifier Agent 机械核查。
/// 固定等级只是外壳，这份带证据的画像才是内核（DESIGN-weakness-profile §1）。
/// </summary>
public sealed class WeaknessProfile
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? AssessmentId { get; set; }
    public string ModelProfileId { get; set; } = "local-dev";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<ProfileFinding> Findings { get; set; } = [];

    public User? User { get; set; }
    public Assessment? Assessment { get; set; }
}

/// <summary>
/// 画像条目：五要素 = 维度 + 强弱 + 结论 + 证据引用（EvidenceJson）+ 置信度。
/// Verification=Questioned 的条目不展示、不进规划输入。
/// </summary>
public sealed class ProfileFinding
{
    public long Id { get; set; }
    public long ProfileId { get; set; }
    public FindingDimension Dimension { get; set; }
    /// <summary>维度内标识：场景 key（如 dining_out）/ 技能名（grammar 等）/ reading。</summary>
    public string DimensionKey { get; set; } = string.Empty;
    public FindingPolarity Polarity { get; set; }
    public string Statement { get; set; } = string.Empty;
    /// <summary>证据引用列表（EvidenceClaim JSON），可回溯到真实记录。</summary>
    public string EvidenceJson { get; set; } = "[]";
    public FindingConfidence Confidence { get; set; }
    public FindingVerification Verification { get; set; } = FindingVerification.Verified;
    public string VerificationNote { get; set; } = string.Empty;

    public WeaknessProfile? Profile { get; set; }
}
