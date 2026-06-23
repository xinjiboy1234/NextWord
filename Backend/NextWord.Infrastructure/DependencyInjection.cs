using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Services;
using NextWord.Infrastructure.Background;
using NextWord.Infrastructure.Caching;
using NextWord.Infrastructure.Data;
using NextWord.Infrastructure.Repositories;
using NextWord.Infrastructure.Services;
using OpenAI.Chat;

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
                // 迁移快照按 PostgreSQL 设计；SQLite 开发库跳过 pending model 严格校验。
                options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
            }
        });

        services.AddScoped<IWordRepository, WordRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IReviewQueueService, ReviewQueueService>();
        services.AddScoped<ISentenceService, SentenceService>();
        services.AddScoped<IFreeExpressionService, FreeExpressionService>();
        services.AddScoped<ISpellingService, SpellingService>();
        services.AddScoped<IArticleService, ArticleService>();
        services.AddScoped<IArticleVocabService, ArticleVocabService>();
        services.AddScoped<ICommentService, CommentService>();
        services.AddScoped<IReadingAgentService, ReadingAssistantAgent>();
        services.AddSingleton<IAssessmentScoringService, AssessmentScoringService>();
        services.AddSingleton<ILevelEngine, LevelUpgradeEngine>();
        services.AddScoped<IChallengePackGenerator, ChallengePackGenerator>();
        services.AddScoped<IAssessmentService, AssessmentService>();
        services.AddScoped<IChallengeService, ChallengeService>();
        services.AddScoped<LevelDashboardService>();
        services.AddSingleton<ISm2Service, Sm2Service>();
        RegisterCache(services, configuration);
        services.AddSingleton<IModelProfileResolver, ModelProfileResolver>();
        services.AddSingleton<LlmMockProvider>();

        var openAiOptions = new LlmOpenAiOptions
        {
            Enabled = bool.TryParse(configuration["Llm:OpenAI:Enabled"], out var enabled) && enabled,
            Model = configuration["Llm:OpenAI:Model"] ?? "gpt-4o-mini",
            ApiKey = configuration["Llm:OpenAI:ApiKey"],
            ApiKeyEnvironmentVariable = configuration["Llm:OpenAI:ApiKeyEnvironmentVariable"] ?? "OPENAI_API_KEY"
        };
        openAiOptions.ApiKey ??= configuration[openAiOptions.ApiKeyEnvironmentVariable];
        openAiOptions.ApiKey ??= Environment.GetEnvironmentVariable(openAiOptions.ApiKeyEnvironmentVariable);
        if (openAiOptions.Enabled && !string.IsNullOrWhiteSpace(openAiOptions.ApiKey))
        {
            services.AddSingleton<IChatClient>(_ => new ChatClient(openAiOptions.Model, openAiOptions.ApiKey).AsIChatClient());
            services.AddSingleton<LlmChatClientProvider>();
            services.AddSingleton<ILLMProvider>(sp => WrapLlmProvider(sp, sp.GetRequiredService<LlmChatClientProvider>()));
        }
        else
        {
            services.AddSingleton<ILLMProvider>(sp => WrapLlmProvider(sp, sp.GetRequiredService<LlmMockProvider>()));
        }

        services.AddHostedService<ReviewReminderWorker>();
        services.AddHostedService<LevelCheckWorker>();

        return services;
    }

    private static void RegisterCache(IServiceCollection services, IConfiguration configuration)
    {
        var cacheProvider = configuration["Cache:Provider"] ?? "Memory";
        if (string.Equals(cacheProvider, "Redis", StringComparison.OrdinalIgnoreCase))
        {
            var redisConnection = configuration.GetConnectionString("Redis")
                ?? configuration["Cache:Redis:ConnectionString"]
                ?? "localhost:6379";
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
                options.InstanceName = configuration["Cache:Redis:InstanceName"] ?? "nextword:";
            });
            services.AddSingleton<ICacheService, RedisCacheService>();
            return;
        }

        services.AddSingleton<ICacheService, MemoryCacheService>();
    }

    private static ILLMProvider WrapLlmProvider(IServiceProvider sp, ILLMProvider inner)
    {
        var retried = new LlmRetryProvider(inner);
        return new LlmTelemetryProvider(retried, sp.GetRequiredService<ILogger<LlmTelemetryProvider>>());
    }
}
