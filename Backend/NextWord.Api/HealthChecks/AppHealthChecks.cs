using Microsoft.Extensions.Diagnostics.HealthChecks;
using NextWord.Infrastructure.Data;

namespace NextWord.Api.HealthChecks;

public sealed class DbHealthCheck(ApplicationDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            return canConnect ? HealthCheckResult.Healthy("Database reachable.") : HealthCheckResult.Unhealthy("Database unreachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database check failed.", ex);
        }
    }
}

public sealed class LlmHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // MVP：Mock/OpenAI 配置均视为可用；真实探测可在后续迭代补充
        return Task.FromResult(HealthCheckResult.Healthy("LLM provider registered."));
    }
}
