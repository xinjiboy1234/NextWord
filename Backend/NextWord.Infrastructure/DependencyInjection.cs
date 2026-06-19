using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
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
        services.AddSingleton<ICacheService, MemoryCacheService>();
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
            services.AddSingleton<ILLMProvider>(sp => new LlmRetryProvider(sp.GetRequiredService<LlmChatClientProvider>()));
        }
        else
        {
            services.AddSingleton<ILLMProvider>(sp => new LlmRetryProvider(sp.GetRequiredService<LlmMockProvider>()));
        }

        services.AddHostedService<ReviewReminderWorker>();
        services.AddHostedService<LevelCheckWorker>();

        return services;
    }
}
