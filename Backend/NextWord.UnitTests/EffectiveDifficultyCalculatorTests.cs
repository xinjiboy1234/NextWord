using NextWord.Domain.Entities;
using NextWord.Domain.Models;
using NextWord.Domain.Services;

namespace NextWord.UnitTests;

public class EffectiveDifficultyCalculatorTests
{
    [Fact]
    public void Personal_difficulty_takes_priority()
    {
        var relationship = new UserWordRelationship { PersonalDifficulty = 72 };
        var result = EffectiveDifficultyCalculator.Compute(12, null, relationship, null);
        Assert.Equal(72, result.Score);
        Assert.Equal(EffectiveDifficultySource.Personal, result.Source);
    }

    [Fact]
    public void High_known_rate_lowers_effective_difficulty()
    {
        var relationship = new UserWordRelationship { EstimatedKnownRate = 0.9 };
        var result = EffectiveDifficultyCalculator.Compute(40, null, relationship, null);
        Assert.True(result.Score < 40);
        Assert.Equal(EffectiveDifficultySource.Computed, result.Source);
    }

    [Fact]
    public void Academic_register_increases_effective_difficulty()
    {
        var baseline = EffectiveDifficultyCalculator.Compute(40, null, null, null).Score;
        var academic = EffectiveDifficultyCalculator.Compute(
            40,
            null,
            null,
            new ReadingDifficultyContext("academic")).Score;
        Assert.Equal(8, academic - baseline);
    }

    [Theory]
    [InlineData(-10)]
    [InlineData(150)]
    public void Result_is_clamped_0_to_100(int intrinsic)
    {
        var result = EffectiveDifficultyCalculator.Compute(intrinsic, null, null, null);
        Assert.InRange(result.Score, 0, 100);
    }

    [Fact]
    public void Legacy_score_used_when_intrinsic_missing()
    {
        var result = EffectiveDifficultyCalculator.Compute(null, 55, null, null);
        Assert.Equal(55, result.Score);
        Assert.Equal(EffectiveDifficultySource.Legacy, result.Source);
    }
}
