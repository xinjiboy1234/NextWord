using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Domain.Services;
using NextWord.Infrastructure.Auth;
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
        var connectionString = configuration.GetConnectionString("PostgreSql")
            ?? "Host=localhost;Port=5432;Database=nextword;Username=nextword;Password=nextword";

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
            // T-015 已收口迁移链（快照与模型一致）；保留忽略仅作迭代期间补丁先行的防御。
            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IWordRepository, WordRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.Configure<AuthOptions>(configuration.GetSection("Auth"));
        services.Configure<LlmSentenceRatingOptions>(configuration.GetSection(LlmSentenceRatingOptions.SectionName));
        services.Configure<ScoreMappingOptions>(configuration.GetSection(ScoreMappingOptions.SectionName));
        services.Configure<ChallengeThresholdsOptions>(configuration.GetSection(ChallengeThresholdsOptions.SectionName));
        services.Configure<SearchOptions>(configuration.GetSection(SearchOptions.SectionName));
        services.AddSingleton<IWebSearchService>(sp => new DuckDuckGoSearchService(
            new HttpClient { Timeout = TimeSpan.FromSeconds(8) },
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SearchOptions>>(),
            sp.GetRequiredService<ILogger<DuckDuckGoSearchService>>()));
        services.AddScoped<ILearningToolHandler, ProfileScoresToolHandler>();
        services.AddScoped<ILearningToolHandler, SearchWebToolHandler>();
        services.AddScoped<ILearningToolHandler, ReadingLookupToolHandler>();
        services.AddScoped<ILearningToolHandler, DailyWordsToolHandler>();
        services.AddScoped<ILearningToolHandler, EvaluationLatestToolHandler>();
        services.AddScoped<ILearningToolHandler, ChallengeRecentToolHandler>();
        services.AddScoped<ILearningToolHandler, RecentLearningToolHandler>();
        services.AddScoped<ILearningToolRegistry, LearningToolRegistry>();
        services.AddSingleton<IScoreMappingService>(sp =>
            new ScoreMappingService(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ScoreMappingOptions>>().Value));
        services.AddScoped<IScoreProfileService, ScoreProfileService>();
        services.AddScoped<IBackgroundJobService, BackgroundJobService>();
        services.AddScoped<IEvaluationReportService, EvaluationReportService>();
        services.AddScoped<EvaluationDataAssembler>();
        services.AddScoped<IWeaknessProfiler, WeaknessProfiler>();
        services.AddScoped<IFindingVerifier, FindingVerifier>();
        services.AddScoped<IWeaknessProfileService, WeaknessProfileService>();
        services.AddScoped<ILearningPlanService, LearningPlanService>();
        services.AddScoped<PlannerWorker>();
        services.AddScoped<IBottleneckScreeningService, BottleneckScreeningService>();
        services.AddScoped<IBottleneckInsightService, BottleneckInsightService>();
        services.AddScoped<IColdStartExplorationService, ColdStartExplorationService>();
        services.AddScoped<BottleneckInsightWorker>();
        services.AddScoped<ReAnnotationWorker>();
        services.AddScoped<ScenarioAnnotationWorker>();
        services.AddScoped<PracticeScoreWritebackService>();
        services.AddScoped<IReadingLookupService, ReadingLookupService>();
        services.AddScoped<IDailyWordSelectionService, DailyWordSelectionService>();
        services.AddScoped<IUserFeedbackService, UserFeedbackService>();
        services.AddScoped<IUserLlmProviderFactory, UserLlmProviderFactory>();
        services.AddScoped<IReviewQueueService, ReviewQueueService>();
        services.AddScoped<ISentenceService, SentenceService>();
        services.AddScoped<IFreeExpressionService, FreeExpressionService>();
        services.AddScoped<ISpellingService, SpellingService>();
        services.AddScoped<IArticleService, ArticleService>();
        services.AddScoped<IArticleVocabService, ArticleVocabService>();
        services.AddScoped<ICommentService, CommentService>();
        services.AddScoped<IReadingAgentService, ReadingAssistantAgent>();
        services.AddSingleton<IAssessmentScoringService>(sp =>
            new AssessmentScoringService(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ScoreMappingOptions>>().Value));
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
            ApiKeyEnvironmentVariable = configuration["Llm:OpenAI:ApiKeyEnvironmentVariable"] ?? "OPENAI_API_KEY",
            BaseUrl = configuration["Llm:OpenAI:BaseUrl"]
        };
        openAiOptions.ApiKey ??= configuration[openAiOptions.ApiKeyEnvironmentVariable];
        openAiOptions.ApiKey ??= Environment.GetEnvironmentVariable(openAiOptions.ApiKeyEnvironmentVariable);
        if (openAiOptions.Enabled && !string.IsNullOrWhiteSpace(openAiOptions.ApiKey))
        {
            services.AddSingleton<IChatClient>(_ => string.IsNullOrWhiteSpace(openAiOptions.BaseUrl)
                ? new ChatClient(openAiOptions.Model, openAiOptions.ApiKey).AsIChatClient()
                : LlmClientFactory.CreateChatClient(openAiOptions.Model, openAiOptions.ApiKey, openAiOptions.BaseUrl));
            services.AddSingleton<LlmChatClientProvider>();
            services.AddSingleton<ILLMProvider>(sp => WrapLlmProvider(sp, sp.GetRequiredService<LlmChatClientProvider>()));
        }
        else
        {
            services.AddSingleton<ILLMProvider>(sp => WrapLlmProvider(sp, sp.GetRequiredService<LlmMockProvider>()));
        }

        services.AddHostedService<ReviewReminderWorker>();
        services.AddHostedService<LevelCheckWorker>();
        services.AddHostedService<ProfileScoreSnapshotWorker>();
        services.AddHostedService<WeeklyReplanWorker>();
        services.AddHostedService<BackgroundJobWorker>();

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
