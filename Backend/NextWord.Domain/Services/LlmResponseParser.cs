using NextWord.Domain.Enums;
using NextWord.Domain.Models;
using System.Text.Json;

namespace NextWord.Domain.Services;

public static class LlmResponseParser
{
    public static DifficultyRating EnsureValid(DifficultyRating rating)
    {
        if (rating.Confidence is < 0 or > 1)
        {
            throw new InvalidOperationException("LLM confidence must be between 0 and 1.");
        }

        return rating;
    }

    public static SentenceRatingResponse ParseSentenceRating(string content)
    {
        var json = ExtractJson(content);
        var parsed = JsonSerializer.Deserialize<SentenceRatingJson>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("LLM returned empty sentence rating.");
        return parsed.ToResponse();
    }

    public static VocabExtractResponse ParseVocabExtract(string content)
    {
        var json = ExtractJson(content);
        var parsed = JsonSerializer.Deserialize<VocabExtractJson>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("LLM returned empty vocab extraction.");
        return new VocabExtractResponse(
            parsed.KeyVocab.Select(item => new KeyVocabItem(
                item.Word,
                item.ContextMeaning,
                item.SpecialUsage,
                ParseDifficulty(item.Difficulty),
                ParseAction(item.Action))).ToList(),
            parsed.SkippedBasic,
            parsed.SkippedRare);
    }

    private static DifficultyLevel ParseDifficulty(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "intermediate" => DifficultyLevel.Intermediate,
            "advanced" => DifficultyLevel.Advanced,
            _ => DifficultyLevel.Basic
        };

    private static RecommendedAction ParseAction(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "review_later" => RecommendedAction.ReviewLater,
            "challenge_only" => RecommendedAction.ChallengeOnly,
            _ => RecommendedAction.LearnNow
        };

    private static string ExtractJson(string content)
    {
        var trimmed = content.Trim();
        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            throw new InvalidOperationException("LLM response did not contain a JSON object.");
        }

        return trimmed[start..(end + 1)];
    }
}
