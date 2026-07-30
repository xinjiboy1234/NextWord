using NextWord.Domain.Models;

namespace NextWord.Domain.Services;

/// <summary>
/// T-027：造句/自由表达评分的「尺子」解析——评分请求必须带用户当前水平带。
/// 优先级：UserProgress 投影出的 CefrDisplay（ScoreMapping 单一来源，含分数推出）
/// → 调用方显式传入的带（测评/挑战路径）→ 默认带（匿名/无进度用户）。
/// </summary>
public static class RatingBandResolver
{
    public const string DefaultBand = "A2";

    public static string Resolve(UserProfileScores scores, string? fallback)
    {
        var hasProgress = scores.Vocabulary is not null
            || scores.Reading is not null
            || scores.Writing is not null
            || scores.UpdatedAt is not null;
        if (hasProgress && !string.IsNullOrWhiteSpace(scores.CefrDisplay))
        {
            return scores.CefrDisplay;
        }

        return string.IsNullOrWhiteSpace(fallback) ? DefaultBand : fallback.Trim();
    }
}
