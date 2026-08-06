namespace NextWord.Domain.Models;

public sealed record ProfileUpdateCommand(
    Guid UserId,
    string Source,
    ProfileScoreAssignment? Absolute,
    ProfileScoreDelta? Delta,
    string IdempotencyKey,
    string? PayloadJson = null,
    /// <summary>T-038：权威定级写入（测评完成）置 true——cefrDisplay 不走下行迟滞，首测/复测定级是权威锚点。</summary>
    bool BypassCefrDisplayHysteresis = false);

public sealed record ProfileScoreAssignment(
    int? Vocabulary,
    int? Reading,
    int? Writing,
    int? Spelling);

public sealed record ProfileScoreDelta(
    int? Vocabulary,
    int? Reading,
    int? Writing,
    int? Spelling);

public sealed record ProfileUpdateResult(
    UserProfileScores Scores,
    bool Applied,
    string? SkipReason = null);
