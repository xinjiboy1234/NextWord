using NextWord.Domain.Entities;

namespace NextWord.Domain.Interfaces;

public interface IFreeExpressionService
{
    /// <summary>
    /// 自由表达评分。T-027：评分尺子取用户当前水平带（UserProgress 投影优先，userLevel 仅作回退）。
    /// T-034：返回本次评分触发毕业的词（自发使用 → spontaneous_use），供前端毕业时刻提示。
    /// </summary>
    Task<FreeExpressionRatingResult> RateAsync(Guid userId, string userText, string? userLevel, CancellationToken cancellationToken);
}

/// <summary>自由表达评分结果：评分日志 + T-034 本次毕业词（lemma 列表，无毕业为空）。</summary>
public sealed record FreeExpressionRatingResult(FreeExpressionLog Log, IReadOnlyList<string> GraduatedWords);
