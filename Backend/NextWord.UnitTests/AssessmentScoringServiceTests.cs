using NextWord.Domain.Enums;
using NextWord.Domain.Services;

namespace NextWord.UnitTests;

/// <summary>T-004：表达力综合分作主叙事，废弃最短板 min；识别映射仅作参考。</summary>
public class AssessmentScoringServiceTests
{
    private readonly AssessmentScoringService _service = new();

    [Theory]
    [InlineData(5, 5, 5, 5, 100.0)]
    [InlineData(0, 0, 0, 0, 0.0)]
    [InlineData(5, 5, 0, 0, 60.0)] // 语法+自然度占 0.6 权重
    [InlineData(0, 0, 5, 5, 40.0)] // 词汇+相关度占 0.4 权重
    [InlineData(3, 3, 2, 2, 52.0)]
    public void Production_dimensions_are_weighted(int grammar, int natural, int vocabulary, int relevance, double expected)
    {
        Assert.Equal(expected, _service.ScoreProductionDimensions(grammar, natural, vocabulary, relevance));
    }

    [Theory]
    [InlineData(15, CefrLevel.A1)]
    [InlineData(25, CefrLevel.A2)]
    [InlineData(40, CefrLevel.B1)]
    [InlineData(60, CefrLevel.B2)]
    [InlineData(90, CefrLevel.C1)]
    public void Expression_score_maps_to_cefr(double composite, CefrLevel expected)
    {
        Assert.Equal(expected, _service.MapExpressionScore(composite));
    }

    [Theory]
    [InlineData(70, BandMove.Up)]
    [InlineData(60, BandMove.Up)]
    [InlineData(59.9, BandMove.Stay)]
    [InlineData(50, BandMove.Stay)]
    [InlineData(39.9, BandMove.Down)]
    public void Band_move_follows_block_expression(double blockExpression, BandMove expected)
    {
        Assert.Equal(expected, _service.DecideBandMove(blockExpression));
    }

    [Theory]
    [InlineData(1, BandMove.Stay, false)]  // 至少 2 块
    [InlineData(2, BandMove.Stay, true)]   // 稳定即收敛
    [InlineData(2, BandMove.Up, false)]    // 仍在移动则再探一块
    [InlineData(3, BandMove.Up, true)]     // 最多 3 块
    public void Convergence_rules(int completedBlocks, BandMove lastMove, bool expected)
    {
        Assert.Equal(expected, _service.ShouldConverge(completedBlocks, lastMove));
    }

    [Theory]
    [InlineData(80, CefrLevel.C1)]
    [InlineData(35, CefrLevel.B1)]
    [InlineData(5, CefrLevel.A1)]
    public void Vocab_accuracy_maps_to_cefr(double accuracy, CefrLevel expected)
    {
        Assert.Equal(expected, _service.MapVocabAccuracy(accuracy));
    }
}
