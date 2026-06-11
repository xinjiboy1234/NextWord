using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Services;
using NextWord.Infrastructure.Data;
using NextWord.Infrastructure.Repositories;

namespace NextWord.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNextWordInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "Sqlite";
        var connectionString = configuration.GetConnectionString(provider) ?? "Data Source=nextword-dev.db";

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            if (string.Equals(provider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
            {
                options.UseNpgsql(connectionString);
            }
            else
            {
                options.UseSqlite(connectionString);
            }
        });

        services.AddScoped<IWordRepository, WordRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IReviewQueueService, ReviewQueueService>();
        services.AddSingleton<ISm2Service, Sm2Service>();
        services.AddSingleton<IModelProfileResolver, ModelProfileResolver>();
        services.AddSingleton<ILLMProvider, LlmMockProvider>();

        return services;
    }
}
