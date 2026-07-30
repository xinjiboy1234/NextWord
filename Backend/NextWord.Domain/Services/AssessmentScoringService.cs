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

    /// <summary>块表现决策：≥60 升带，&lt;40 降带，其余保持（T-009：65→60，低带中等偏上答案块均分约 61–65，65 摸不到）。</summary>
    public BandMove DecideBandMove(double blockExpressionScore) =>
        blockExpressionScore >= 60 ? BandMove.Up : blockExpressionScore < 40 ? BandMove.Down : BandMove.Stay;

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
