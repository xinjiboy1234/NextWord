using NextWord.Domain.Enums;

namespace NextWord.Domain.Models;

/// <summary>
/// 证据引用（T-005）：指向真实数据的一条引用，Verifier 据此机械核查。
/// Kind：sentence_log（RefId=SentenceLog Id，Metric=grammar|natural|vocabulary|relevance）
///     / assessment_dimension（RefId=final，Metric=grammar|natural|vocabulary|relevance|expressionScore）
///     / word_stats（RefId=场景 key，Metric=coverage|avgMastery|correctRate）
///     / reading_stats（RefId=reading，Metric=sessionCount|avgLookupCount）。
/// Metric 为空表示仅引用记录存在性；Op 取 &lt;=、&gt;=、&lt;、&gt;、=。
/// </summary>
public sealed record EvidenceClaim(string Kind, string RefId, string? Metric, string? Op, double? Value);

/// <summary>Profiler 产出的 Finding 草稿（未核查）。</summary>
public sealed record ProfileFindingDraft(
    FindingDimension Dimension,
    string DimensionKey,
    FindingPolarity Polarity,
    string Statement,
    IReadOnlyList<EvidenceClaim> Evidence,
    FindingConfidence Confidence);

/// <summary>Verifier 核查结论。</summary>
public sealed record VerifiedFinding(
    ProfileFindingDraft Draft,
    FindingVerification Verification,
    string Note);

/// <summary>Profiler 可用的造句评分留痕（含可引用的记录 Id）。</summary>
public sealed record SentenceLogEvidence(
    Guid Id,
    string TargetWord,
    string Scene,
    int Grammar,
    int Natural,
    int Vocabulary,
    int Relevance,
    IReadOnlyList<string> ErrorTags);

/// <summary>场景词掌握统计（数值统一保留两位小数，Profiler 引用值与 Verifier 重算值同源）。</summary>
public sealed record ScenarioWordStat(
    string ScenarioKey,
    string ScenarioZh,
    int AnnotatedWords,
    int LearnedWords,
    double Coverage,
    double AvgMastery,
    double CorrectRate,
    int ReviewedSamples);

/// <summary>阅读行为统计。</summary>
public sealed record ReadingBehaviorStat(int SessionCount, double AvgLookupCount);

/// <summary>Profiler Agent 输入：全部来自库内真实数据的聚合快照。</summary>
public sealed record WeaknessProfileRequest(
    string UserLevel,
    AssessmentDimensionSummary? AssessmentDimensions,
    int? ExpressionScore,
    IReadOnlyList<SentenceLogEvidence> SentenceLogs,
    IReadOnlyList<ScenarioWordStat> ScenarioStats,
    ReadingBehaviorStat Reading,
    LlmRequestOptions? Options = null);

/// <summary>Profiler Agent 产出。</summary>
public sealed record WeaknessProfileResponse(IReadOnlyList<ProfileFindingDraft> Findings);
