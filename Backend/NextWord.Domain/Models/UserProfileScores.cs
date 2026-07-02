namespace NextWord.Domain.Models;

public sealed record UserProfileScores(
    int? Vocabulary,
    int? Reading,
    int? Writing,
    int? Spelling,
    int Overall,
    string DifficultyBucket,
    string? CefrDisplay,
    DateTimeOffset? UpdatedAt);
