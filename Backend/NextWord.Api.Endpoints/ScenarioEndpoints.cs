using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Scenarios;
using NextWord.Infrastructure.Data;

namespace NextWord.Api.Endpoints;

/// <summary>
/// 场景 taxonomy 查询与场景标注任务触发（设计方案 §6 验收 1/4）。
/// </summary>
public static class ScenarioEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/scenarios").WithTags("Scenarios");

        // taxonomy + 每子场景有效词数 + core 桶词数
        group.MapGet("/", async (ApplicationDbContext db, CancellationToken ct) =>
        {
            var counts = await db.WordScenarios
                .GroupBy(item => item.ScenarioKey)
                .Select(group => new { group.Key, Count = group.Count() })
                .ToDictionaryAsync(group => group.Key, group => group.Count, ct);
            // core 通用桶 = 已标注且 0 个子场景的词
            var coreCount = await db.Words
                .Where(word => word.ScenarioAnnotationVersion >= Infrastructure.Services.ScenarioAnnotationWorker.CurrentVersion
                    && !word.Scenarios.Any())
                .CountAsync(ct);

            var categories = ScenarioTaxonomy.Categories.Select(category => new
            {
                key = category.Key,
                zhName = category.ZhName,
                subScenarios = ScenarioTaxonomy.All
                    .Where(item => item.CategoryKey == category.Key)
                    .Select(item => new
                    {
                        key = item.Key,
                        zhName = item.ZhName,
                        wordCount = counts.GetValueOrDefault(item.Key)
                    })
            });

            return Results.Ok(new { categories, coreBucketWordCount = coreCount });
        });

        // 触发批量场景标注：幂等（同小时重复触发复用同一 job），失败/跑完可在下一时段重跑续标
        group.MapPost("/annotation-jobs", async (
            int? batchSize,
            ApplicationDbContext db,
            IBackgroundJobService backgroundJobs,
            CancellationToken ct) =>
        {
            var unannotated = await db.Words
                .CountAsync(word => word.ScenarioAnnotationVersion < Infrastructure.Services.ScenarioAnnotationWorker.CurrentVersion, ct);
            var jobId = await backgroundJobs.EnqueueAsync(
                Infrastructure.Services.ScenarioAnnotationWorker.JobType,
                System.Text.Json.JsonSerializer.Serialize(new { batchSize = Math.Clamp(batchSize ?? 20, 1, 50) }),
                $"scenario-annotation:bulk:{DateTimeOffset.UtcNow:yyyyMMddHH}",
                ct);
            return Results.Accepted($"/api/scenarios/annotation-jobs/{jobId}", new { jobId, unannotated });
        });
    }
}
