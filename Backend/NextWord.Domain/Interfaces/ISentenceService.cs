using NextWord.Domain.Entities;

namespace NextWord.Domain.Interfaces;

public interface ISentenceService
{
    Task<IReadOnlyList<Sentence>> GetPromptsAsync(int count, CancellationToken cancellationToken);
    /// <summary>
    /// T-006：个性化出题。有当日 Plan 用 Plan 造句目标（FromPlan=true）；
    /// 无 Plan/过期回退带内约束选词（产出任务只用水平带内的词，VISION §4.3）。
    /// </summary>
    Task<SentencePromptBatch> GetPersonalizedPromptsAsync(Guid userId, int count, CancellationToken cancellationToken);
    /// <summary>
    /// 造句评分。T-027：评分手里的尺子是用户当前水平带——优先取 UserProgress 投影（ScoreMapping），
    /// userLevel 仅为无进度时的回退（测评/挑战路径显式传带），空则回退默认带。
    /// </summary>
    Task<SentenceLog> RateAsync(Guid userId, Guid? wordId, string targetWord, string userSentence, string scene, string? userLevel, CancellationToken cancellationToken);
}

/// <summary>出题批次：FromPlan 标记是否来自当日 LearningPlan 造句目标。</summary>
public sealed record SentencePromptBatch(IReadOnlyList<Sentence> Prompts, bool FromPlan);
