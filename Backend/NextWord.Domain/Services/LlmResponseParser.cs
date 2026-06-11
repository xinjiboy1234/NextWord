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
