using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Interfaces;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

public sealed class BackgroundJobService(ApplicationDbContext db) : IBackgroundJobService
{
    public async Task<long> EnqueueAsync(string jobType, string payloadJson, string idempotencyKey, CancellationToken cancellationToken)
    {
        var existing = await db.BackgroundJobs.AsNoTracking()
            .FirstOrDefaultAsync(job => job.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return existing.Id;
        }

        var job = new BackgroundJob
        {
            JobType = jobType,
            PayloadJson = payloadJson,
            IdempotencyKey = idempotencyKey,
            Status = "Pending"
        };
        db.BackgroundJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        return job.Id;
    }

    public async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        var jobs = await db.BackgroundJobs
            .Where(job => job.Status == "Pending")
            .OrderBy(job => job.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var job in jobs)
        {
            job.Status = "Processing";
        }

        if (jobs.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
