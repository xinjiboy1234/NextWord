using NextWord.Domain.Entities;

namespace NextWord.Domain.Interfaces;

public interface IFreeExpressionService
{
    /// <summary>
    /// 自由表达评分。T-027：评分尺子取用户当前水平带（UserProgress 投影优先，userLevel 仅作回退）。
    /// </summary>
    Task<FreeExpressionLog> RateAsync(Guid userId, string userText, string? userLevel, CancellationToken cancellationToken);
}
