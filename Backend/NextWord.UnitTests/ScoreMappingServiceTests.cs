using NextWord.Domain.Models;
using NextWord.Domain.Services;

namespace NextWord.UnitTests;

public class ScoreMappingServiceTests
{
    private static ScoreMappingService CreateService() => new(new ScoreMappingOptions());

    [Theory]
    [InlineData(0, "A1")]
    [InlineData(19, "A1")]
    [InlineData(20, "A2")]
    [InlineData(35, "B1")]
    [InlineData(50, "B1")]  // T-023：B1 带扩到 70
    [InlineData(69, "B1")]
    [InlineData(70, "B2")]  // T-023：B2 起点 70
    [InlineData(85, "C1")]  // T-023：C1 起点 85
    [InlineData(95, "C2")]
    [InlineData(100, "C2")]
    public void MapToCefr_uses_band_boundaries(int score, string expected)
    {
        Assert.Equal(expected, CreateService().MapToCefr(score));
    }

    [Theory]
    [InlineData(0, "Basic")]
    [InlineData(34, "Basic")]
    [InlineData(35, "Intermediate")]
    [InlineData(69, "Intermediate")]
    [InlineData(70, "Advanced")]
    [InlineData(100, "Advanced")]
    public void MapToBucket_uses_difficulty_buckets(int score, string expected)
    {
        Assert.Equal(expected, CreateService().MapToBucket(score));
    }

    [Fact]
    public void ComputeOverall_uses_shortest_board_and_ignores_spelling()
    {
        var service = CreateService();
        Assert.Equal(53, service.ComputeOverall(74, 53, 61));
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(150, 100)]
    public void ClampScore_bounds_0_to_100(int input, int expected)
    {
        Assert.Equal(expected, CreateService().ClampScore(input));
    }
}
