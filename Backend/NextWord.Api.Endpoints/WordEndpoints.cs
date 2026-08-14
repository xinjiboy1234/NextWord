using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Infrastructure.Data;

namespace NextWord.Api.Endpoints;

public static class WordEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/words").WithTags("Words");

        group.MapGet("/", async (string? scenario, IWordRepository words, CancellationToken ct) =>
        {
            var result = await words.ListAsync(ct);
            if (!string.IsNullOrWhiteSpace(scenario))
            {
                result = result
                    .Where(word => word.Scenarios.Any(item => string.Equals(item.ScenarioKey, scenario, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            return Results.Ok(result.Select(WordDto.FromEntity));
        });

        // T-034：当前用户已毕业（spontaneous_use）词列表——Dashboard 本周毕业计数与词库「已毕业」标记共用
        group.MapGet("/graduated", async (HttpContext http, IUserRepository users, ApplicationDbContext db, CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(http, null, users, ct);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            var graduated = await db.UserWordRelationships.AsNoTracking()
                .Include(item => item.Word)
                .Where(item => item.UserId == user.Id && item.LifecycleStage == WordLifecycleStage.SpontaneousUse)
                .OrderByDescending(item => item.StageUpdatedAt)
                .ToListAsync(ct);
            return Results.Ok(graduated
                .Select(item => new GraduatedWordDto(item.WordId, item.Word?.Lemma ?? string.Empty, item.StageUpdatedAt))
                .ToList());
        });

        group.MapGet("/daily", async (HttpContext http, int? count, IUserRepository users, IDailyWordSelectionService dailyWords, CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(http, null, users, ct);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            // T-050：默认每日词量 10→15（上限 20 不变，前端可选 10/15/20）
            var result = await dailyWords.GetDailyAsync(user.Id, Math.Clamp(count ?? 15, 1, 20), ct);
            return Results.Ok(result);
        });

        group.MapGet("/{id:guid}", async (Guid id, IWordRepository words, CancellationToken ct) =>
        {
            var word = await words.GetByIdAsync(id, ct);
            return word is null ? Results.NotFound() : Results.Ok(WordDto.FromEntity(word));
        });

        group.MapPost("/", async (CreateWordRequest request, IWordRepository words, ILLMProvider llm, IBackgroundJobService backgroundJobs, CancellationToken ct) =>
        {
            var existing = await words.GetByLemmaAsync(request.Lemma, ct);
            if (existing is not null)
            {
                return Results.Conflict(new { message = "Word already exists.", word = WordDto.FromEntity(existing) });
            }

            var rating = await llm.RateDifficultyAsync(new(ItemType.Word, request.Lemma), ct);
            var lemma = request.Lemma.Trim().ToLowerInvariant();
            var word = new Word
            {
                Lemma = lemma,
                PartOfSpeech = request.PartOfSpeech.Trim(),
                Phonetics = request.Phonetics.Trim(),
                Meanings = request.Meanings.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).ToList(),
                ExampleSentences = request.ExampleSentences.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).ToList(),
                DifficultyLevel = rating.DifficultyLevel,
                CefrLevel = rating.CefrLevel,
                IsCore = request.IsCore
            };
            await words.AddAsync(word, ct);
            await words.SaveChangesAsync(ct);
            // 新词异步场景标注（ScenarioAnnotationWorker 幂等：已标注词自动跳过）
            await backgroundJobs.EnqueueAsync(
                "ScenarioAnnotation",
                "{}",
                $"scenario-annotation:word:{lemma}",
                ct);
            return Results.Created($"/api/words/{word.Id}", WordDto.FromEntity(word));
        });
    }
}

public sealed record CreateWordRequest(
    string Lemma,
    string PartOfSpeech,
    string Phonetics,
    IReadOnlyList<string> Meanings,
    IReadOnlyList<string> ExampleSentences,
    bool IsCore = true);

/// <summary>T-034：已毕业词（spontaneous_use）。GraduatedAt 取阶段流转时间（毕业当刻）。</summary>
public sealed record GraduatedWordDto(Guid WordId, string Lemma, DateTimeOffset? GraduatedAt);

public sealed record WordDto(
    Guid Id,
    string Lemma,
    string PartOfSpeech,
    string Phonetics,
    IReadOnlyList<string> Meanings,
    IReadOnlyList<string> ExampleSentences,
    DifficultyLevel DifficultyLevel,
    CefrLevel CefrLevel,
    bool IsCore,
    IReadOnlyList<string> Scenarios,
    WordUtility? Utility,
    ExpressionRole? Role)
{
    public static WordDto FromEntity(Word word)
    {
        return new WordDto(
            word.Id,
            word.Lemma,
            word.PartOfSpeech,
            word.Phonetics,
            word.Meanings,
            word.ExampleSentences,
            word.DifficultyLevel,
            word.CefrLevel,
            word.IsCore,
            word.Scenarios.Select(item => item.ScenarioKey).ToList(),
            word.Utility,
            word.Role);
    }
}
