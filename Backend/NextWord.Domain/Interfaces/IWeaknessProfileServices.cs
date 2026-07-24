using NextWord.Domain.Entities;
using NextWord.Domain.Models;

namespace NextWord.Domain.Interfaces;

/// <summary>
/// WeaknessProfile 画像服务（T-005）：测评完成后由评估报告任务触发，
/// Profiler 生成 Finding 草稿 → Verifier 机械核查 → 持久化；同一测评幂等（已生成则直接返回）。
/// </summary>
public interface IWeaknessProfileService
{
    Task<WeaknessProfile> GenerateAsync(Guid userId, Guid? assessmentId, CancellationToken cancellationToken);
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
/// </summary>
public interface IFindingVerifier
{
    Task<IReadOnlyList<VerifiedFinding>> VerifyAsync(
        Guid userId,
        Guid? assessmentId,
        IReadOnlyList<ProfileFindingDraft> drafts,
        CancellationToken cancellationToken);
}
