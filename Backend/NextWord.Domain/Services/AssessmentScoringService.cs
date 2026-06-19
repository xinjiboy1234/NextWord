using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;

namespace NextWord.Domain.Services;

/// <summary>
/// 测评评分与 CEFR 映射。规则来自 Phase 3 计划中的映射表，短板定级取 min。
/// </summary>
public sealed class AssessmentScoringService : IAssessmentScoringService
{
    public CefrLevel MapVocabAccuracy(double accuracyPercent) => MapByThresholds(accuracyPercent, [9, 29, 49, 69]);

    public CefrLevel MapSpellingAccuracy(double accuracyPercent) => MapByThresholds(accuracyPercent, [0, 19, 39, 59]);

    public CefrLevel MapSentenceAverage(double averageScore) => MapByThresholds(averageScore, [0.9, 1.9, 2.9, 3.9]);

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

    public FinalLevelResult CalculateFinalLevel(
        StepScoreResult vocab,
        StepScoreResult spelling,
        StepScoreResult sentence,
        StepScoreResult reading)
    {
        var vocabLevel = vocab.MappedLevel ?? CefrLevel.A1;
        var spellingLevel = spelling.MappedLevel ?? CefrLevel.A1;
        var sentenceLevel = sentence.MappedLevel ?? CefrLevel.A1;
        var readingLevel = reading.MappedLevel ?? CefrLevel.A1;
        var overall = MinLevel(vocabLevel, sentenceLevel, readingLevel);
        return new FinalLevelResult(vocabLevel, spellingLevel, sentenceLevel, readingLevel, overall);
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

    private static CefrLevel MinLevel(CefrLevel a, CefrLevel b, CefrLevel c)
    {
        var min = (int)Math.Min((int)a, Math.Min((int)b, (int)c));
        return (CefrLevel)min;
    }

    private static CefrLevel GetPrevious(CefrLevel level) =>
        level <= CefrLevel.A1 ? CefrLevel.A1 : (CefrLevel)((int)level - 1);
}
