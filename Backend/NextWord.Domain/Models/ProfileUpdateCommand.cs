namespace NextWord.Domain.Models;

public sealed record ProfileUpdateCommand(
    Guid UserId,
    string Source,
    ProfileScoreAssignment? Absolute,
    ProfileScoreDelta? Delta,
    string IdempotencyKey,
    string? PayloadJson = null);

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
