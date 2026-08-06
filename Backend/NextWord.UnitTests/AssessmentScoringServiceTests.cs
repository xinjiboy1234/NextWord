using NextWord.Domain.Enums;
using NextWord.Domain.Models;
using NextWord.Domain.Services;

namespace NextWord.UnitTests;

/// <summary>T-004：表达力综合分作主叙事，废弃最短板 min；识别映射仅作参考。</summary>
public class AssessmentScoringServiceTests
{
    private readonly AssessmentScoringService _service = new(new ScoreMappingOptions());

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
    [InlineData(60, CefrLevel.B1)]  // T-023：新分带 B1 上限 70
    [InlineData(90, CefrLevel.C1)]
    public void Expression_score_maps_to_cefr(double composite, CefrLevel expected)
    {
        Assert.Equal(expected, _service.MapExpressionScore(composite));
    }

    /// <summary>T-023：定级校准锚点——四维 3.2/5（综合 64）定 B1；均分 4.0/5（80）以上进 B2；B2 起点 70 边界 ±1。</summary>
    [Theory]
    [InlineData(64, CefrLevel.B1)]
    [InlineData(80, CefrLevel.B2)]
    [InlineData(69, CefrLevel.B1)]
    [InlineData(70, CefrLevel.B2)]
    [InlineData(100, CefrLevel.C1)] // 测评封顶 C1
    public void Expression_score_maps_to_cefr_t023_calibration(double composite, CefrLevel expected)
    {
        Assert.Equal(expected, _service.MapExpressionScore(composite));
    }

    /// <summary>T-042：升带阈值 60→70——65 不再升带，75 升带；降带 &lt;40 不变。</summary>
    [Theory]
    [InlineData(75, BandMove.Up)]
    [InlineData(70, BandMove.Up)]
    [InlineData(69.9, BandMove.Stay)]
    [InlineData(65, BandMove.Stay)]
    [InlineData(60, BandMove.Stay)]
    [InlineData(39.9, BandMove.Down)]
    public void Band_move_follows_block_expression(double blockExpression, BandMove expected)
    {
        Assert.Equal(expected, _service.DecideBandMove(blockExpression));
    }

    /// <summary>T-042 识别防伪闸：表达定级档 − 词汇识别参考档 ≥2 下调 1 档；档差不足、样本缺失、反向均不矫正。</summary>
    [Theory]
    [InlineData(CefrLevel.B2, CefrLevel.A1, CefrLevel.B1, true)]  // 仿真菜鸟剧本：表达 B2 + 识别 A1 → 下调
    [InlineData(CefrLevel.C1, CefrLevel.A2, CefrLevel.B2, true)]  // 档差 3 → 只下调 1 档（一次性）
    [InlineData(CefrLevel.B2, CefrLevel.B1, CefrLevel.B2, false)] // 档差 1 不矫正
    [InlineData(CefrLevel.A2, CefrLevel.A1, CefrLevel.A2, false)] // 档差 1 不矫正
    [InlineData(CefrLevel.A2, CefrLevel.C1, CefrLevel.A2, false)] // 反向（识别高于表达）不矫正
    [InlineData(CefrLevel.A1, CefrLevel.A1, CefrLevel.A1, false)] // A1 为下限
    [InlineData(CefrLevel.B2, null, CefrLevel.B2, false)]         // 识别样本缺失不矫正
    public void Recognition_guard_adjusts_only_when_gap_at_least_two(
        CefrLevel expressionLevel, CefrLevel? vocabReferenceLevel, CefrLevel expectedLevel, bool expectedAdjusted)
    {
        var (level, adjusted) = _service.ApplyRecognitionGuard(expressionLevel, vocabReferenceLevel);
        Assert.Equal(expectedLevel, level);
        Assert.Equal(expectedAdjusted, adjusted);
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

    /// <summary>T-042 矫正传导：分数带上限（含）= 分带 Max − 1，供先验 clamp 进矫正后档。</summary>
    [Theory]
    [InlineData(CefrLevel.A1, 19)]
    [InlineData(CefrLevel.B1, 69)]
    [InlineData(CefrLevel.B2, 84)]
    public void Band_score_ceiling_follows_score_mapping(CefrLevel level, int expected)
    {
        Assert.Equal(expected, _service.GetBandScoreCeiling(level));
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
