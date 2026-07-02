using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Interfaces;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

public sealed class LearningToolRegistry(
    IEnumerable<ILearningToolHandler> handlers) : ILearningToolRegistry
{
    private readonly IReadOnlyDictionary<string, ILearningToolHandler> _handlers =
        handlers.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> ToolNames => _handlers.Keys.OrderBy(item => item).ToList();

    public Task<object> InvokeAsync(string toolName, JsonElement args, Guid userId, CancellationToken cancellationToken)
    {
        if (!_handlers.TryGetValue(toolName, out var handler))
        {
            throw new InvalidOperationException($"Unknown tool: {toolName}");
        }

        return handler.ExecuteAsync(args, userId, cancellationToken);
    }
}

public sealed class ProfileScoresToolHandler(IScoreProfileService scoreProfile) : ILearningToolHandler
{
    public string Name => "get_profile_scores";

    public async Task<object> ExecuteAsync(JsonElement args, Guid userId, CancellationToken cancellationToken)
        => await scoreProfile.GetScoresAsync(userId, cancellationToken);
}

public sealed class SearchWebToolHandler(IWebSearchService search) : ILearningToolHandler
{
    public string Name => "search_web";

    public async Task<object> ExecuteAsync(JsonElement args, Guid userId, CancellationToken cancellationToken)
    {
        var query = args.TryGetProperty("query", out var queryEl) ? queryEl.GetString() ?? string.Empty : string.Empty;
        var results = await search.SearchAsync(query, cancellationToken);
        return results;
    }
}

public sealed class ReadingLookupToolHandler(IReadingLookupService lookup) : ILearningToolHandler
{
    public string Name => "lookup_word_context";

    public async Task<object> ExecuteAsync(JsonElement args, Guid userId, CancellationToken cancellationToken)
    {
        var word = args.GetProperty("word").GetString() ?? string.Empty;
        var sentence = args.TryGetProperty("sentence", out var sentenceEl) ? sentenceEl.GetString() ?? string.Empty : string.Empty;
        Guid? articleId = args.TryGetProperty("articleId", out var articleEl) && articleEl.ValueKind == JsonValueKind.String
            && Guid.TryParse(articleEl.GetString(), out var parsed)
            ? parsed
            : null;
        return await lookup.LookupAsync(userId, new ReadingLookupRequest(word, sentence, articleId), cancellationToken);
    }
}

public sealed class DailyWordsToolHandler(IDailyWordSelectionService dailyWords) : ILearningToolHandler
{
    public string Name => "get_daily_words";

    public async Task<object> ExecuteAsync(JsonElement args, Guid userId, CancellationToken cancellationToken)
    {
        var count = args.TryGetProperty("count", out var countEl) ? countEl.GetInt32() : 10;
        return await dailyWords.GetDailyAsync(userId, count, cancellationToken);
    }
}

public sealed class EvaluationLatestToolHandler(ApplicationDbContext db) : ILearningToolHandler
{
    public string Name => "get_evaluation_latest";

    public async Task<object> ExecuteAsync(JsonElement args, Guid userId, CancellationToken cancellationToken)
    {
        return await db.EvaluationReports.AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? (object)new { status = "none" };
    }
}

public sealed class ChallengeRecentToolHandler(ApplicationDbContext db) : ILearningToolHandler
{
    public string Name => "get_challenge_history";

    public async Task<object> ExecuteAsync(JsonElement args, Guid userId, CancellationToken cancellationToken)
    {
        var limit = args.TryGetProperty("limit", out var limitEl) ? limitEl.GetInt32() : 5;
        return await db.ChallengeRecords.AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.Timestamp)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}

public sealed class RecentLearningToolHandler(ApplicationDbContext db) : ILearningToolHandler
{
    public string Name => "get_recent_learning";

    public async Task<object> ExecuteAsync(JsonElement args, Guid userId, CancellationToken cancellationToken)
    {
        var limit = args.TryGetProperty("limit", out var limitEl) ? limitEl.GetInt32() : 10;
        return await db.WordLearningLogs.AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.Timestamp)
            .Take(limit)
            .Select(item => new { item.WordId, item.Rating, item.IsCorrect, item.Timestamp })
            .ToListAsync(cancellationToken);
    }
}
