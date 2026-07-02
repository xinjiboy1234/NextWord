using NextWord.Domain.Entities;
using NextWord.Domain.Models;

namespace NextWord.Domain.Services;

public static class EffectiveDifficultyCalculator
{
    private const int KnownRateAdjustSpan = 20;
    private const int KnownRateAdjustOffset = 10;
    private const int AcademicRegisterBoost = 8;

    public static EffectiveDifficultyResult Compute(
        int? intrinsicScore,
        int? legacyDifficultyScore,
        UserWordRelationship? relationship,
        ReadingDifficultyContext? context)
    {
        if (relationship?.PersonalDifficulty is int personal)
        {
            return new EffectiveDifficultyResult(Clamp(personal), EffectiveDifficultySource.Personal);
        }

        if (intrinsicScore is int intrinsic)
        {
            var knownRate = relationship?.EstimatedKnownRate ?? 0.5;
            var knownAdjust = (int)((1 - knownRate) * KnownRateAdjustSpan);
            var score = intrinsic + knownAdjust - KnownRateAdjustOffset;
            if (string.Equals(context?.Register, "academic", StringComparison.OrdinalIgnoreCase))
            {
                score += AcademicRegisterBoost;
            }

            return new EffectiveDifficultyResult(Clamp(score), EffectiveDifficultySource.Computed);
        }

        if (legacyDifficultyScore is int legacy)
        {
            return new EffectiveDifficultyResult(Clamp(legacy), EffectiveDifficultySource.Legacy);
        }

        return new EffectiveDifficultyResult(50, EffectiveDifficultySource.Heuristic);
    }

    public static int Clamp(int score) => Math.Clamp(score, 0, 100);
}
