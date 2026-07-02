namespace NextWord.Domain.Models;

public sealed class ChallengeThresholdsOptions
{
    public const string SectionName = "ChallengeThresholds";

    public double VocabAccuracyMin { get; set; } = 0.60;
    public int WritingScoreMin { get; set; } = 53;
    public int ReadingScoreMin { get; set; } = 100;
    public int UpgradeDelta { get; set; } = 5;
}
