using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Models;

namespace NextWord.Domain.Services;

/// <summary>
/// 画像变化规则 diff（T-036，DESIGN-monthly-timeline §2-3）：当前 WeaknessProfile 与上一份对比，
/// 产出「新增的强项」与「好转的弱点」，纯规则、零 LLM。存疑（Questioned）条目不参与对比。
/// </summary>
public static class ProfileChangeDiffer
{
    public static ProfileChangeDiff Diff(
        IReadOnlyList<ProfileFinding> previous,
        IReadOnlyList<ProfileFinding> current)
    {
        var prev = previous.Where(item => item.Verification != FindingVerification.Questioned).ToList();
        var curr = current.Where(item => item.Verification != FindingVerification.Questioned).ToList();

        // 新增的强项：当前为强项，且上一份同维度位不是强项（不存在或由弱点/中立转强）
        var newStrengths = curr
            .Where(item => item.Polarity == FindingPolarity.Strength)
            .Where(item => !prev.Any(other => SameKey(other, item) && other.Polarity == FindingPolarity.Strength))
            .Select(item => new ProfileChangeItem(item.Dimension, item.DimensionKey, item.Statement))
            .ToList();

        // 好转的弱点：上一份为弱点，当前同维度位已不再是弱点（转强项/中立或不再上榜）
        var improvedWeaknesses = prev
            .Where(item => item.Polarity == FindingPolarity.Weakness)
            .Where(item => !curr.Any(other => SameKey(other, item) && other.Polarity == FindingPolarity.Weakness))
            .Select(item =>
            {
                var now = curr.FirstOrDefault(other => SameKey(other, item));
                return new ProfileChangeItem(item.Dimension, item.DimensionKey, now?.Statement ?? item.Statement);
            })
            .ToList();

        return new ProfileChangeDiff(newStrengths, improvedWeaknesses);
    }

    private static bool SameKey(ProfileFinding left, ProfileFinding right)
        => left.Dimension == right.Dimension
            && string.Equals(left.DimensionKey, right.DimensionKey, StringComparison.OrdinalIgnoreCase);
}

/// <summary>画像 diff 结果（T-036）。</summary>
public sealed record ProfileChangeDiff(
    IReadOnlyList<ProfileChangeItem> NewStrengths,
    IReadOnlyList<ProfileChangeItem> ImprovedWeaknesses);
