using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Interfaces;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

/// <summary>
/// WeaknessProfile 画像服务（T-005）：测评完成后触发一次（评估报告后台任务），
/// Profiler 草稿 → Verifier 核查 → 持久化；同一测评幂等，重复触发直接返回已生成画像。
/// </summary>
public sealed class WeaknessProfileService(
    ApplicationDbContext db,
    IWeaknessProfiler profiler,
    IFindingVerifier verifier) : IWeaknessProfileService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<WeaknessProfile> GenerateAsync(Guid userId, Guid? assessmentId, CancellationToken cancellationToken)
    {
        var existing = await db.WeaknessProfiles
            .Include(profile => profile.Findings)
            .Where(profile => profile.UserId == userId && profile.AssessmentId == assessmentId)
            .OrderByDescending(profile => profile.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var response = await profiler.BuildDraftsAsync(userId, assessmentId, cancellationToken);
        var verified = await verifier.VerifyAsync(userId, assessmentId, response.Findings, cancellationToken);

        var profile = new WeaknessProfile
        {
            UserId = userId,
            AssessmentId = assessmentId,
            ModelProfileId = "weakness-profile"
        };
        foreach (var item in verified)
        {
            profile.Findings.Add(new ProfileFinding
            {
                Dimension = item.Draft.Dimension,
                DimensionKey = item.Draft.DimensionKey,
                Polarity = item.Draft.Polarity,
                Statement = item.Draft.Statement,
                EvidenceJson = JsonSerializer.Serialize(item.Draft.Evidence, JsonOptions),
                Confidence = item.Draft.Confidence,
                Verification = item.Verification,
                VerificationNote = item.Note
            });
        }

        db.WeaknessProfiles.Add(profile);
        await db.SaveChangesAsync(cancellationToken);
        return profile;
    }

    public Task<WeaknessProfile?> GetLatestAsync(Guid userId, CancellationToken cancellationToken)
    {
        return db.WeaknessProfiles.AsNoTracking()
            .Include(profile => profile.Findings)
            .Where(profile => profile.UserId == userId)
            .OrderByDescending(profile => profile.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
