namespace NextWord.Domain.Models;

public sealed class ChallengeThresholdsOptions
{
    public const string SectionName = "ChallengeThresholds";

    public double VocabAccuracyMin { get; set; } = 0.60;
    public int WritingScoreMin { get; set; } = 53;

    /// <summary>阅读阈值：T-035 起阅读 3 题按正确率映射（0/33/67/100），答对 2 题即过线。</summary>
    public int ReadingScoreMin { get; set; } = 67;
    public int UpgradeDelta { get; set; } = 5;
}
