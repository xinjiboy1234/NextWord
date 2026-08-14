using NextWord.Domain.Enums;

namespace NextWord.Domain.Services;

/// <summary>rubric 四维（与测评产出题四维评分口径一致）。</summary>
public enum RubricDimension
{
    Grammar = 1,
    Natural = 2,
    Vocabulary = 3,
    Relevance = 4
}

/// <summary>
/// T-055 人话水平 rubric（DESIGN-assessment-visibility §3.1，R3）：规则映射、零 LLM。
/// 把分数翻译成用户能懂的水平描述；文案常量集中此处，便于后续迭代措辞。
/// 总体标签按 CEFR 带映射——分带派生复用 <see cref="AssessmentScoringService.MapExpressionScore"/>
/// （ScoreMapping:CefrBands 单一数据源，此处不复制分带数字），本类只负责「带 → 人话」。
/// 错误标签（TopErrorTags）本身是 LLM 按解释语言（默认 zh-CN）生成的中文自由文本，无需映射表，直接展示。
/// </summary>
public static class ProficiencyRubric
{
    /// <summary>总体水平标签：CEFR 带 → 人话标签 + 一句话描述（C1/C2 同档「很溜」）。</summary>
    public static OverallRubric DescribeOverall(CefrLevel level) => level switch
    {
        CefrLevel.A1 => new OverallRubric("起步", "能蹦出单词和简单短句，离成段表达还有距离"),
        CefrLevel.A2 => new OverallRubric("粗糙", "能表达基本意思，但错误较多、句式单一"),
        CefrLevel.B1 => new OverallRubric("凑合", "日常简单表达能应付，复杂一点就吃力"),
        CefrLevel.B2 => new OverallRubric("还不错", "表达清楚连贯，偶有用词不当和不自然"),
        _ => new OverallRubric("很溜", "表达自然丰富，接近母语者的灵活度")
    };

    /// <summary>维度中文名（展示用，不出现英文 key）。</summary>
    public static string DimensionName(RubricDimension dimension) => dimension switch
    {
        RubricDimension.Grammar => "语法",
        RubricDimension.Natural => "自然度",
        RubricDimension.Vocabulary => "词汇",
        _ => "相关度"
    };

    /// <summary>四维特征描述：0–5 分三档（≤2 弱 / 3 中 / ≥4 强；小数取所属区间）。</summary>
    public static string DescribeDimension(RubricDimension dimension, double score)
    {
        var tier = score >= 4 ? Tier.Strong : score <= 2 ? Tier.Weak : Tier.Mid;
        return (dimension, tier) switch
        {
            (RubricDimension.Grammar, Tier.Weak) => "句子结构常出错，时态单复数混乱",
            (RubricDimension.Grammar, Tier.Mid) => "大结构正确，小错不断",
            (RubricDimension.Grammar, _) => "稳定正确，能驾驭复合句",
            (RubricDimension.Natural, Tier.Weak) => "中式表达明显，句子生硬",
            (RubricDimension.Natural, Tier.Mid) => "能看懂但不像地道说法",
            (RubricDimension.Natural, _) => "表达自然地道",
            (RubricDimension.Vocabulary, Tier.Weak) => "用词单调重复，有用词不当",
            (RubricDimension.Vocabulary, Tier.Mid) => "日常词够用但笼统",
            (RubricDimension.Vocabulary, _) => "用词准确有层次",
            (RubricDimension.Relevance, Tier.Weak) => "表达过于简洁或答非所问",
            (RubricDimension.Relevance, Tier.Mid) => "切题但内容偏单薄",
            _ => "切题且言之有物"
        };
    }

    private enum Tier
    {
        Weak,
        Mid,
        Strong
    }
}

/// <summary>总体人话标签（T-055）：<see cref="ProficiencyRubric.DescribeOverall"/> 的输出。</summary>
public sealed record OverallRubric(string Label, string Description);
