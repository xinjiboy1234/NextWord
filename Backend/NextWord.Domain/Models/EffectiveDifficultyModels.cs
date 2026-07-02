namespace NextWord.Domain.Models;

public sealed record ReadingDifficultyContext(string? Register);

public enum EffectiveDifficultySource
{
    Personal,
    Computed,
    Intrinsic,
    Legacy,
    Heuristic
}

public sealed record EffectiveDifficultyResult(
    int Score,
    EffectiveDifficultySource Source);
