using NextWord.Domain.Entities;
using NextWord.Domain.Models;

namespace NextWord.Domain.Interfaces;

/// <summary>
/// WeaknessProfile 画像服务（T-005）：测评完成后由评估报告任务触发，
/// Profiler 生成 Finding 草稿 → Verifier 机械核查 → 持久化；同一测评幂等（已生成则直接返回）。
/// T-007：AssessmentId 为空时（事件驱动重规划）按日幂等——同日只重生成一次。
/// T-032：coldStart = true 为冷启动重生成（探索周触发，每用户仅一次）——Verifier 走放宽档，
/// 画像以 ModelProfileId = "weakness-profile-coldstart" 落标记位，与瓶颈触发的重生成区分。
/// </summary>
public interface IWeaknessProfileService
{
    Task<WeaknessProfile> GenerateAsync(Guid userId, Guid? assessmentId, CancellationToken cancellationToken, bool coldStart = false);
    Task<WeaknessProfile?> GetLatestAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>Profiler Agent：聚合库内真实数据 → LLM 产出 Finding 草稿。</summary>
public interface IWeaknessProfiler
{
    Task<WeaknessProfileResponse> BuildDraftsAsync(Guid userId, Guid? assessmentId, CancellationToken cancellationToken);
}

/// <summary>
/// Verifier Agent：对每条 Finding 机械核查（不调用 LLM、不做主观改写）——
/// 证据引用真实存在且属于该用户、引用数值属实、样本量支撑置信度；不通过标「存疑」。
/// T-032：relaxedColdStart = true 为冷启动放宽档（仅首份冷启动重生成画像）——
/// 证据真实、数值一致但条数不足的 Finding 置信下调 low 标 Verified 并注明「初步判断」；
/// 伪造/越权/数值不符的机械核查不放宽。第二份画像起（默认 false）恢复既有纪律。
/// </summary>
public interface IFindingVerifier
{
    Task<IReadOnlyList<VerifiedFinding>> VerifyAsync(
        Guid userId,
        Guid? assessmentId,
        IReadOnlyList<ProfileFindingDraft> drafts,
        CancellationToken cancellationToken,
        bool relaxedColdStart = false);
}
