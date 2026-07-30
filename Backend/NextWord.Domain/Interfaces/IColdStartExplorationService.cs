using NextWord.Domain.Models;

namespace NextWord.Domain.Interfaces;

/// <summary>
/// 画像冷启动「探索周」（T-032，DESIGN-cold-start-profile）：注册起 7 天为探索周，
/// 每日编排 1 个轻量场景表达任务（走既有 free-expression 链路）攒产出证据；
/// 满 7 天或产出证据（SentenceLogs + FreeExpressionLogs）≥10 条且从未做过冷启动重生成
/// → 触发 WeaknessProfileService.GenerateAsync(assessmentId: null, coldStart: true) + 强制重规划，每用户仅一次。
/// 触发判断抽成纯服务，供 ProfileScoreSnapshotWorker 日检与单测复用。
/// </summary>
public interface IColdStartExplorationService
{
    /// <summary>探索周进度（第 x/7 天、还差 N 条证据、今日表达任务），供 Dashboard 计划卡展示。</summary>
    Task<ExplorationWeekStatus> GetExplorationWeekAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>冷启动画像重生成触发判定（纯判断，无副作用）。</summary>
    Task<ColdStartTriggerEvaluation> EvaluateTriggerAsync(Guid userId, CancellationToken cancellationToken);
}
