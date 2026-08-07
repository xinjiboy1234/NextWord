using NextWord.Domain.Models;
using NextWord.Infrastructure.Services;

namespace NextWord.Api.Endpoints;

/// <summary>
/// 「我的这个月」月度时间轴端点（T-036，DESIGN-monthly-timeline §2-2）：
/// 只读聚合——本月里程碑（词毕业/挑战首过/定级升级/画像生成）+ 画像变化 + 洞察回放，零 LLM、不写状态。
/// </summary>
public static class ProfileTimelineEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/profile").WithTags("Profile");

        group.MapGet("/monthly-timeline", async (
            HttpContext http,
            int? days,
            MonthlyTimelineService timeline,
            CancellationToken ct) =>
        {
            var userId = UserResolver.GetAuthenticatedUserId(http);
            if (!userId.HasValue)
            {
                return Results.Unauthorized();
            }

            var window = Math.Clamp(days ?? 30, 1, 365);
            var result = await timeline.GetAsync(userId.Value, window, ct);

            return Results.Ok(new MonthlyTimelineDto(
                result.Days,
                result.Events.Select(item => new MonthlyTimelineEventDto(
                    item.Type,
                    item.OccurredAt,
                    item.Word,
                    item.Level,
                    item.FromLevel,
                    item.ToLevel,
                    item.Reason)).ToList(),
                new MonthlyProfileChangeDto(
                    result.ProfileChange.HasProfile,
                    result.ProfileChange.HasComparison,
                    result.ProfileChange.CurrentProfileAt,
                    result.ProfileChange.NewStrengths.Select(ToDto).ToList(),
                    result.ProfileChange.ImprovedWeaknesses.Select(ToDto).ToList(),
                    result.ProfileChange.CurrentFindings.Select(item => new ProfileFindingSummaryDto(
                        item.Dimension.ToString().ToLowerInvariant(),
                        item.DimensionKey,
                        item.Polarity.ToString().ToLowerInvariant(),
                        item.Statement)).ToList()),
                result.Insights.Select(item => new MonthlyInsightDto(
                    item.Nature,
                    item.Statement,
                    item.CreatedAt)).ToList()));
        }).RequireAuthorization();
    }

    private static ProfileChangeItemDto ToDto(ProfileChangeItem item) =>
        new(item.Dimension.ToString().ToLowerInvariant(), item.DimensionKey, item.Statement);
}

public sealed record MonthlyTimelineDto(
    int Days,
    IReadOnlyList<MonthlyTimelineEventDto> Events,
    MonthlyProfileChangeDto ProfileChange,
    IReadOnlyList<MonthlyInsightDto> Insights);

public sealed record MonthlyTimelineEventDto(
    string Type,
    DateTimeOffset OccurredAt,
    string? Word,
    string? Level,
    string? FromLevel,
    string? ToLevel,
    string? Reason);

public sealed record MonthlyProfileChangeDto(
    bool HasProfile,
    bool HasComparison,
    DateTimeOffset? CurrentProfileAt,
    IReadOnlyList<ProfileChangeItemDto> NewStrengths,
    IReadOnlyList<ProfileChangeItemDto> ImprovedWeaknesses,
    IReadOnlyList<ProfileFindingSummaryDto> CurrentFindings);

public sealed record ProfileChangeItemDto(string Dimension, string DimensionKey, string Statement);

public sealed record ProfileFindingSummaryDto(string Dimension, string DimensionKey, string Polarity, string Statement);

public sealed record MonthlyInsightDto(string Nature, string Statement, DateTimeOffset CreatedAt);
