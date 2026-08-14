using NextWord.Domain.Enums;
using NextWord.Domain.Models;
using NextWord.Domain.Services;

namespace NextWord.UnitTests;

/// <summary>
/// T-055 人话 rubric（DESIGN-assessment-visibility §3.1 / §4-2）：
/// 五个分带 → 五个标签（含分带交界边界值）；四维各档（≤2/3/≥4）→ 对应描述。
/// 分带派生复用 AssessmentScoringService.MapExpressionScore（ScoreMapping:CefrBands 单一数据源）。
/// </summary>
public class ProficiencyRubricTests
{
    private readonly AssessmentScoringService _scoring = new(new ScoreMappingOptions());

    [Theory]
    [InlineData(CefrLevel.A1, "起步", "能蹦出单词和简单短句，离成段表达还有距离")]
    [InlineData(CefrLevel.A2, "粗糙", "能表达基本意思，但错误较多、句式单一")]
    [InlineData(CefrLevel.B1, "凑合", "日常简单表达能应付，复杂一点就吃力")]
    [InlineData(CefrLevel.B2, "还不错", "表达清楚连贯，偶有用词不当和不自然")]
    [InlineData(CefrLevel.C1, "很溜", "表达自然丰富，接近母语者的灵活度")]
    [InlineData(CefrLevel.C2, "很溜", "表达自然丰富，接近母语者的灵活度")]
    public void Overall_label_maps_from_cefr_band(CefrLevel level, string expectedLabel, string expectedDescription)
    {
        var rubric = ProficiencyRubric.DescribeOverall(level);
        Assert.Equal(expectedLabel, rubric.Label);
        Assert.Equal(expectedDescription, rubric.Description);
    }

    /// <summary>分带交界边界值：表达综合分 →（ScoreMapping:CefrBands）→ 带 → 人话标签，全链路断言。</summary>
    [Theory]
    [InlineData(0, "起步")]
    [InlineData(19.9, "起步")]
    [InlineData(20, "粗糙")]   // A1/A2 交界
    [InlineData(34.9, "粗糙")]
    [InlineData(35, "凑合")]   // A2/B1 交界
    [InlineData(69.9, "凑合")]
    [InlineData(70, "还不错")] // B1/B2 交界
    [InlineData(84.9, "还不错")]
    [InlineData(85, "很溜")]   // B2/C1 交界
    [InlineData(100, "很溜")]
    public void Overall_label_follows_score_band_boundaries(double compositeScore, string expectedLabel)
    {
        var level = _scoring.MapExpressionScore(compositeScore);
        Assert.Equal(expectedLabel, ProficiencyRubric.DescribeOverall(level).Label);
    }

    [Theory]
    [InlineData(RubricDimension.Grammar, "语法")]
    [InlineData(RubricDimension.Natural, "自然度")]
    [InlineData(RubricDimension.Vocabulary, "词汇")]
    [InlineData(RubricDimension.Relevance, "相关度")]
    public void Dimension_names_are_chinese(RubricDimension dimension, string expectedName)
    {
        Assert.Equal(expectedName, ProficiencyRubric.DimensionName(dimension));
    }

    /// <summary>四维三档边界：2 落弱档、3 落中档、4 落强档（DESIGN §3.1 表格文案）。</summary>
    [Theory]
    // 语法
    [InlineData(RubricDimension.Grammar, 0, "句子结构常出错，时态单复数混乱")]
    [InlineData(RubricDimension.Grammar, 2, "句子结构常出错，时态单复数混乱")]
    [InlineData(RubricDimension.Grammar, 3, "大结构正确，小错不断")]
    [InlineData(RubricDimension.Grammar, 4, "稳定正确，能驾驭复合句")]
    [InlineData(RubricDimension.Grammar, 5, "稳定正确，能驾驭复合句")]
    // 自然度
    [InlineData(RubricDimension.Natural, 2, "中式表达明显，句子生硬")]
    [InlineData(RubricDimension.Natural, 3, "能看懂但不像地道说法")]
    [InlineData(RubricDimension.Natural, 4, "表达自然地道")]
    // 词汇
    [InlineData(RubricDimension.Vocabulary, 2, "用词单调重复，有用词不当")]
    [InlineData(RubricDimension.Vocabulary, 3, "日常词够用但笼统")]
    [InlineData(RubricDimension.Vocabulary, 4, "用词准确有层次")]
    // 相关度
    [InlineData(RubricDimension.Relevance, 2, "表达过于简洁或答非所问")]
    [InlineData(RubricDimension.Relevance, 3, "切题但内容偏单薄")]
    [InlineData(RubricDimension.Relevance, 4, "切题且言之有物")]
    public void Dimension_description_maps_by_tier(RubricDimension dimension, double score, string expected)
    {
        Assert.Equal(expected, ProficiencyRubric.DescribeDimension(dimension, score));
    }

    /// <summary>小数均分（四维为多题平均）按区间落档：2.5/3.9 为中档。</summary>
    [Theory]
    [InlineData(2.5, "大结构正确，小错不断")]
    [InlineData(3.9, "大结构正确，小错不断")]
    [InlineData(4.0, "稳定正确，能驾驭复合句")]
    public void Dimension_fractional_scores_fall_into_tier(double score, string expected)
    {
        Assert.Equal(expected, ProficiencyRubric.DescribeDimension(RubricDimension.Grammar, score));
    }
}
