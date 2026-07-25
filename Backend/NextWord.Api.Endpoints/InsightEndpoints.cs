using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Infrastructure.Data;
using NextWord.Infrastructure.Services;

namespace NextWord.Api.Endpoints;

/// <summary>
/// 瓶颈洞察端点（T-007）：手动跑指标筛查并在触发时入队 InsightAgent（幂等按日）+ 查询最新洞察。
/// 洞察先服务重规划，不做用户可见展示；这里的只读查询供验收与调试。
/// </summary>
public static class InsightEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/insights").WithTags("Insights");

        group.MapPost("/bottleneck/jobs", async (
            HttpContext http,
            IUserRepository users,
            IBottleneckScreeningService screening,
            IBackgroundJobService backgroundJobs,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(http, null, users, ct);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            var signals = await screening.ScreenAsync(user.Id, ct);
            if (signals.Count == 0)
            {
                return Results.Ok(new { triggered = false, signals = Array.Empty<string>() });
            }

            var jobId = await backgroundJobs.EnqueueAsync(
                BottleneckInsightWorker.JobType,
                JsonSerializer.Serialize(new { userId = user.Id, signals = signals.Select(signal => signal.ToWireName()) }, JsonOptions),
                $"insight:{user.Id}:{DateTimeOffset.UtcNow:yyyyMMdd}",
                ct);
            return Results.Accepted($"/api/insights/bottleneck/jobs/{jobId}", new
            {
                jobId,
                triggered = true,
                signals = signals.Select(signal => signal.ToWireName())
            });
        });

        group.MapGet("/bottleneck/latest", async (
            HttpContext http,
            IUserRepository users,
            ApplicationDbContext db,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(http, null, users, ct);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            var insight = await db.BottleneckInsights.AsNoTracking()
                .Where(item => item.UserId == user.Id)
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefaultAsync(ct);
            if (insight is null)
            {
                return Results.Ok(new { found = false });
            }

            return Results.Ok(new
            {
                found = true,
                nature = insight.Nature.ToString(),
                insight.Signals,
                insight.Statement,
                evidenceLogIds = JsonSerializer.Deserialize<List<Guid>>(insight.EvidenceJson, JsonOptions),
                insight.ReplanTriggered,
                insight.CreatedAt
            });
        });
    }
}
