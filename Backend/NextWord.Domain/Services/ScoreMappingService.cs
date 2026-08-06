using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;

namespace NextWord.Domain.Services;

public sealed class ScoreMappingService(ScoreMappingOptions options) : IScoreMappingService
{
    public string? MapToCefr(int score) => MapBandLabel(options.CefrBands, ClampScore(score));

    public string MapToBucket(int score) =>
        MapBandLabel(options.DifficultyBuckets, ClampScore(score)) ?? "Basic";

    public UserProfileScores Project(UserProgress progress)
    {
        var overall = ComputeOverall(progress.VocabularyScore, progress.ReadingScore, progress.WritingScore);
        var bucket = progress.DifficultyBucket ?? MapToBucket(overall);
        var cefr = progress.CefrDisplay ?? MapToCefr(overall);
        return new UserProfileScores(
            progress.VocabularyScore,
            progress.ReadingScore,
            progress.WritingScore,
            progress.SpellingScore,
            overall,
            bucket,
            cefr,
            progress.ScoresUpdatedAt);
    }

    public int ComputeOverall(int? vocabulary, int? reading, int? writing)
    {
        var v = vocabulary ?? 0;
        var r = reading ?? 0;
        var w = writing ?? 0;
        return Math.Min(v, Math.Min(r, w));
    }

    public int ClampScore(int score) => Math.Clamp(score, 0, 100);

    public CefrLevel MapScoreToCefrLevel(int score)
    {
        var label = MapToCefr(score);
        return label is not null && Enum.TryParse<CefrLevel>(label, out var level) ? level : CefrLevel.A1;
    }

    public ScoreBand? GetCefrBand(string label) =>
        options.CefrBands.FirstOrDefault(band => string.Equals(band.Label, label, StringComparison.OrdinalIgnoreCase));

    private static string? MapBandLabel(IReadOnlyList<ScoreBand> bands, int score)
    {
        foreach (var band in bands)
        {
            var isLast = band == bands[^1];
            var inBand = isLast
                ? score >= band.Min && score <= band.Max
                : score >= band.Min && score < band.Max;
            if (inBand)
            {
                return band.Label;
            }
        }

        return bands.Count > 0 ? bands[^1].Label : null;
    }
}
