using NextWord.Domain.Models;

namespace NextWord.Domain.Services;

/// <summary>
/// T-035（DESIGN-challenge-outcome §2.1）：Daily 挑战通过后的即时画像点评。
/// 纯规则生成、零 LLM：本次三维得分对比 Profile 三维分，指出最长板与最短板；不改分数。
/// </summary>
public static class ChallengeFeedback
{
    /// <summary>与 Profile 分差达到该值才说「高出一截 / 拖了后腿」，避免噪声点评。</summary>
    public const int MeaningfulGap = 10;

    public static string Build(int vocabularyScore, int writingScore, int readingScore, UserProfileScores? profile)
    {
        var dimensions = new[]
        {
            (Label: "词汇", Score: vocabularyScore, Baseline: profile?.Vocabulary),
            (Label: "写作", Score: writingScore, Baseline: profile?.Writing),
            (Label: "阅读", Score: readingScore, Baseline: profile?.Reading),
        };

        var strongest = dimensions.OrderByDescending(item => item.Score).First();
        var weakest = dimensions.OrderBy(item => item.Score).First();

        if (strongest.Label == weakest.Label)
        {
            return $"三维表现齐平，{strongest.Label}稳在 {strongest.Score}，继续冲。";
        }

        var strongPart = strongest.Baseline is int highBaseline && strongest.Score >= highBaseline + MeaningfulGap
            ? $"{strongest.Label}比你的当前水平高出一截，继续冲"
            : $"{strongest.Label}是你的最长板，继续保持";
        var weakPart = weakest.Baseline is int lowBaseline && weakest.Score < lowBaseline - MeaningfulGap
            ? $"{weakest.Label}拖了后腿，多补一补"
            : $"{weakest.Label}还有提升空间";

        return $"{strongPart}；{weakPart}。";
    }
}
