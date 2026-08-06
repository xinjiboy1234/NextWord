using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;

namespace NextWord.Domain.Services;

/// <summary>
/// 测评评分与 CEFR 映射（T-004 重构，DESIGN-assessment-rework §2.2）：
/// 主定级由表达力综合分决定（产出题四维加权），识别分仅作参考展示，废弃最短板 min。
/// MapXxxToScore 系列同时被挑战流（ChallengeService）沿用。
/// </summary>
public sealed class AssessmentScoringService(ScoreMappingOptions options) : IAssessmentScoringService
{
    /// <summary>块表现升带阈值（T-042：60→70，慷慨评分下 60 等于不设防；降带 &lt;40 不变）。</summary>
    public const double BandUpThreshold = 70;

    /// <summary>块表现降带阈值（不含）。</summary>
    public const double BandDownThreshold = 40;

    /// <summary>识别防伪闸档差（T-042，DESIGN-assessment-anti-inflation §2.2）：表达定级档 − 词汇识别参考档达到此值即下调 1 档。</summary>
    public const int RecognitionGuardBandGap = 2;

    /// <summary>产出题单题得分：四维加权（语法/自然度权重高于词汇/相关度），0–100。</summary>
    public double ScoreProductionDimensions(int grammar, int natural, int vocabulary, int relevance)
    {
        var weighted = 0.3 * Math.Clamp(grammar, 0, 5)
            + 0.3 * Math.Clamp(natural, 0, 5)
            + 0.2 * Math.Clamp(vocabulary, 0, 5)
            + 0.2 * Math.Clamp(relevance, 0, 5);
        return Math.Round(weighted / 5.0 * 100, 1);
    }

    /// <summary>
    /// 表达力综合分 → CEFR（主定级）。阈值派生自 ScoreMapping:CefrBands（单一来源，T-023 起不再硬编码），
    /// 测评封顶 C1（C2 带不参与测评定级）。
    /// </summary>
    public CefrLevel MapExpressionScore(double compositeScore)
    {
        foreach (var band in options.CefrBands)
        {
            if (!Enum.TryParse<CefrLevel>(band.Label, out var level) || level > CefrLevel.C1)
            {
                continue;
            }

            if (compositeScore < band.Max)
            {
                return level;
            }
        }

        return CefrLevel.C1;
    }

    /// <summary>块表现决策：≥<see cref="BandUpThreshold"/> 升带，&lt;<see cref="BandDownThreshold"/> 降带，其余保持（T-042：升带 60→70）。</summary>
    public BandMove DecideBandMove(double blockExpressionScore) =>
        blockExpressionScore >= BandUpThreshold ? BandMove.Up : blockExpressionScore < BandDownThreshold ? BandMove.Down : BandMove.Stay;

    /// <summary>
    /// 识别防伪闸（T-042，DESIGN-assessment-anti-inflation §2.2）：定级完成后一次性矫正。
    /// 表达定级档 − 词汇识别参考档 ≥ <see cref="RecognitionGuardBandGap"/> 时下调 1 档（下限 A1）；
    /// 识别样本缺失（null）或反向（识别高于表达）不矫正。返回矫正后定级与是否发生矫正。
    /// </summary>
    public (CefrLevel Level, bool Adjusted) ApplyRecognitionGuard(CefrLevel expressionLevel, CefrLevel? vocabReferenceLevel)
    {
        if (vocabReferenceLevel is null
            || (int)expressionLevel - (int)vocabReferenceLevel.Value < RecognitionGuardBandGap
            || expressionLevel <= CefrLevel.A1)
        {
            return (expressionLevel, false);
        }

        return ((CefrLevel)((int)expressionLevel - 1), true);
    }

    /// <summary>指定 CEFR 档的分数带上限（含，= 分带 Max − 1）：T-042 矫正传导——分数先验逐维 clamp 进矫正后档，避免 CefrDisplay 仍按虚高档。</summary>
    public int GetBandScoreCeiling(CefrLevel level)
    {
        foreach (var band in options.CefrBands)
        {
            if (Enum.TryParse<CefrLevel>(band.Label, out var bandLevel) && bandLevel == level)
            {
                return band.Max - 1;
            }
        }

        return 100;
    }

    /// <summary>收敛：最多 3 块；满 2 块且表现稳定（不再升降带）即可收敛，总题量 ≤15。</summary>
    public bool ShouldConverge(int completedBlocks, BandMove lastMove) =>
        completedBlocks >= 3 || (completedBlocks >= 2 && lastMove == BandMove.Stay);

    public CefrLevel MapVocabAccuracy(double accuracyPercent) => MapByThresholds(accuracyPercent, [9, 29, 49, 69]);

    public CefrLevel MapReadingAccuracy(double accuracyPercent, int lookupCount, int wordCount)
    {
        var baseLevel = MapByThresholds(accuracyPercent, [19, 39, 59, 79]);
        // 查词密度过高时下调一级，避免高估阅读能力
        if (wordCount > 0 && lookupCount > wordCount * 0.15 && baseLevel > CefrLevel.A1)
        {
            return GetPrevious(baseLevel);
        }

        return baseLevel;
    }

    public int MapVocabToScore(double accuracyPercent) => Math.Clamp((int)Math.Round(accuracyPercent), 0, 100);

    public int MapSentenceToScore(double averageScore) =>
        Math.Clamp((int)Math.Round(averageScore / 5.0 * 100), 0, 100);

    public int MapReadingToScore(double accuracyPercent, int lookupCount, int wordCount)
    {
        var score = MapVocabToScore(accuracyPercent);
        if (wordCount > 0 && lookupCount > wordCount * 0.15)
        {
            score = Math.Max(0, score - 10);
        }

        return score;
    }

    private static CefrLevel MapByThresholds(double value, double[] upperBoundsExclusive)
    {
        var levels = new[] { CefrLevel.A1, CefrLevel.A2, CefrLevel.B1, CefrLevel.B2, CefrLevel.C1 };
        for (var i = 0; i < upperBoundsExclusive.Length; i++)
        {
            if (value <= upperBoundsExclusive[i])
            {
                return levels[i];
            }
        }

        return CefrLevel.C1;
    }

    private static CefrLevel MapByThresholds(double value, int[] upperBoundsExclusive)
        => MapByThresholds(value, upperBoundsExclusive.Select(item => (double)item).ToArray());

    private static CefrLevel GetPrevious(CefrLevel level) =>
        level <= CefrLevel.A1 ? CefrLevel.A1 : (CefrLevel)((int)level - 1);
}
