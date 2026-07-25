using NextWord.Domain.Entities;
using NextWord.Domain.Enums;

namespace NextWord.Domain.Interfaces;

public interface IArticleService
{
    Task<IReadOnlyList<Article>> ListAsync(DifficultyLevel? level, CefrLevel? cefr, CancellationToken cancellationToken);
    /// <summary>T-006：阅读推荐。有当日 Plan 按主攻场景选文（FromPlan=true）；无 Plan/过期按难度就近回退。</summary>
    Task<RecommendedArticles> GetRecommendedAsync(Guid userId, CancellationToken cancellationToken);
    Task<Article?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ReadingLog> StartReadingAsync(Guid userId, Guid articleId, CancellationToken cancellationToken);
    Task<ReadingLog?> FinishReadingAsync(Guid logId, int lookupCount, int commentsCount, CancellationToken cancellationToken);
    Task IncrementLookupAsync(Guid logId, CancellationToken cancellationToken);
}

/// <summary>阅读推荐结果：FromPlan 标记是否来自当日 LearningPlan。</summary>
public sealed record RecommendedArticles(IReadOnlyList<Article> Articles, bool FromPlan);
