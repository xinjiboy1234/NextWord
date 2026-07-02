using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;

namespace NextWord.Api.Endpoints;

public static class WordEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/words").WithTags("Words");

        group.MapGet("/", async (IWordRepository words, CancellationToken ct) =>
        {
            var result = await words.ListAsync(ct);
            return Results.Ok(result.Select(WordDto.FromEntity));
        });

        group.MapGet("/daily", async (HttpContext http, int? count, IUserRepository users, IDailyWordSelectionService dailyWords, CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(http, null, users, ct);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            var result = await dailyWords.GetDailyAsync(user.Id, Math.Clamp(count ?? 10, 1, 20), ct);
            return Results.Ok(result);
        });

        group.MapGet("/{id:guid}", async (Guid id, IWordRepository words, CancellationToken ct) =>
        {
            var word = await words.GetByIdAsync(id, ct);
            return word is null ? Results.NotFound() : Results.Ok(WordDto.FromEntity(word));
        });

        group.MapPost("/", async (CreateWordRequest request, IWordRepository words, ILLMProvider llm, CancellationToken ct) =>
        {
            var existing = await words.GetByLemmaAsync(request.Lemma, ct);
            if (existing is not null)
            {
                return Results.Conflict(new { message = "Word already exists.", word = WordDto.FromEntity(existing) });
            }

            var rating = await llm.RateDifficultyAsync(new(ItemType.Word, request.Lemma), ct);
            var word = new Word
            {
                Lemma = request.Lemma.Trim().ToLowerInvariant(),
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

public sealed record WordDto(
    Guid Id,
    string Lemma,
    string PartOfSpeech,
    string Phonetics,
    IReadOnlyList<string> Meanings,
    IReadOnlyList<string> ExampleSentences,
    DifficultyLevel DifficultyLevel,
    CefrLevel CefrLevel,
    bool IsCore)
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
            word.IsCore);
    }
}
