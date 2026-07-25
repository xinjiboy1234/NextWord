using System.Text.Json;
using NextWord.Domain.Interfaces;
using NextWord.Infrastructure.Services;

namespace NextWord.Api.Endpoints;

/// <summary>
/// 学习计划端点（T-006）：手动触发当日 Planner 任务（幂等按日）+ 查询当日有效 Plan。
/// 夜间自动生成由评估报告任务触发入队，这里只做触发与只读查询，不做 Plan 编辑。
/// </summary>
public static class PlannerEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/planner").WithTags("Planner");

        group.MapPost("/jobs", async (
            HttpContext http,
            IUserRepository users,
            IBackgroundJobService backgroundJobs,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(http, null, users, ct);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            var jobId = await backgroundJobs.EnqueueAsync(
                PlannerWorker.JobType,
                JsonSerializer.Serialize(new { userId = user.Id }),
                $"planner:{user.Id}:{DateTimeOffset.UtcNow:yyyyMMdd}",
                ct);
            return Results.Accepted($"/api/planner/jobs/{jobId}", new { jobId });
        });

        group.MapGet("/current", async (
            HttpContext http,
            IUserRepository users,
            ILearningPlanService plans,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(http, null, users, ct);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            var active = await plans.GetActiveAsync(user.Id, ct);
            if (active is null)
            {
                return Results.Ok(new { active = false });
            }

            var day = active.DayIndex < active.Content.Days.Count ? active.Content.Days[active.DayIndex] : null;
            return Results.Ok(new
            {
                active = true,
                startDate = active.Plan.StartDate,
                dayIndex = active.DayIndex,
                focusScenarios = active.Content.FocusScenarios,
                sourceFindingIds = active.Content.SourceFindingIds,
                articleIds = active.Content.ArticleIds,
                todayWordCount = day?.WordIds.Count ?? 0,
                todayExposureCount = day?.ExposureWordIds.Count ?? 0,
                todaySentenceTargets = day?.SentenceTargets ?? []
            });
        });
    }
}
