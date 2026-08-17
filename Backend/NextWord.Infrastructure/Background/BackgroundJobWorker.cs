using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NextWord.Domain.Interfaces;
using NextWord.Infrastructure.Data;
using NextWord.Infrastructure.Services;

namespace NextWord.Infrastructure.Background;

public sealed class BackgroundJobWorker(IServiceScopeFactory scopeFactory, ILogger<BackgroundJobWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var evaluation = scope.ServiceProvider.GetRequiredService<IEvaluationReportService>();

                // T-013：先回收僵尸 Processing 任务（进程中断遗留），再捞 Pending
                await StaleJobReclaimer.ReclaimAsync(db, DateTimeOffset.UtcNow, stoppingToken);

                var jobs = (await db.BackgroundJobs
                    .Where(job => job.Status == "Pending")
                    .ToListAsync(stoppingToken))
                    .OrderBy(job => job.CreatedAt)
                    .Take(5)
                    .ToList();

                foreach (var job in jobs)
                {
                    job.Status = "Processing";
                    job.StartedAt = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync(stoppingToken);
                    try
                    {
                        if (job.JobType == "EvaluationReport")
                        {
                            await evaluation.ProcessJobAsync(job, stoppingToken);
                        }
                        else if (job.JobType == "ReAnnotation")
                        {
                            var reannotation = scope.ServiceProvider.GetRequiredService<ReAnnotationWorker>();
                            await reannotation.ProcessAsync(job, stoppingToken);
                        }
                        else if (job.JobType == ScenarioAnnotationWorker.JobType)
                        {
                            var scenarioWorker = scope.ServiceProvider.GetRequiredService<ScenarioAnnotationWorker>();
                            await scenarioWorker.ProcessAsync(job, stoppingToken);
                        }
                        else if (job.JobType == PlannerWorker.JobType)
                        {
                            var planner = scope.ServiceProvider.GetRequiredService<PlannerWorker>();
                            await planner.ProcessAsync(job, stoppingToken);
                        }
                        else if (job.JobType == "AssessmentBlockScoring")
                        {
                            var assessment = scope.ServiceProvider.GetRequiredService<IAssessmentService>();
                            using var payload = JsonDocument.Parse(job.PayloadJson);
                            var assessmentId = payload.RootElement.GetProperty("assessmentId").GetGuid();
                            var blockIndex = payload.RootElement.GetProperty("blockIndex").GetInt32();
                            await assessment.ScoreBlockJobAsync(assessmentId, blockIndex, stoppingToken);
                        }
                        else if (job.JobType == DifficultyAnnotationWorker.JobType)
                        {
                            var difficultyWorker = scope.ServiceProvider.GetRequiredService<DifficultyAnnotationWorker>();
                            await difficultyWorker.ProcessAsync(job, stoppingToken);
                        }
                        else if (job.JobType == BottleneckInsightWorker.JobType)
                        {
                            var insightWorker = scope.ServiceProvider.GetRequiredService<BottleneckInsightWorker>();
                            await insightWorker.ProcessAsync(job, stoppingToken);
                        }

                        job.Status = "Completed";
                        job.ProcessedAt = DateTimeOffset.UtcNow;
                    }
                    catch (Exception ex)
                    {
                        job.Status = "Failed";
                        job.ErrorMessage = ex.Message;
                        job.ProcessedAt = DateTimeOffset.UtcNow;
                        logger.LogError(ex, "Background job {JobId} failed", job.Id);
                    }

                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background job worker loop error");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
