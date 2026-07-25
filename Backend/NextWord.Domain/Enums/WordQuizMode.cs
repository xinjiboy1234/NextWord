namespace NextWord.Domain.Enums;

/// <summary>T-014 背词考察模式（DESIGN-word-lifecycle §4）：按生命周期阶段切换。</summary>
public enum WordQuizMode
{
    /// <summary>看词知义（认识阶段）。</summary>
    Recognition,
    /// <summary>看义想词（回忆及以后阶段）。</summary>
    Recall
}
