using System.Text.Json.Serialization;
using NextWord.Domain.Enums;
using NextWord.Domain.Models;

namespace NextWord.Domain.Services;

public sealed class SentenceRatingJson
{
    [JsonPropertyName("grammar_score")]
    public int GrammarScore { get; set; }

    [JsonPropertyName("natural_score")]
    public int NaturalScore { get; set; }

    [JsonPropertyName("vocabulary_score")]
    public int VocabularyScore { get; set; }

    [JsonPropertyName("relevance_score")]
    public int RelevanceScore { get; set; }

    [JsonPropertyName("overall_grade")]
    public string OverallGrade { get; set; } = "C";

    [JsonPropertyName("ai_revision")]
    public string AiRevision { get; set; } = string.Empty;

    [JsonPropertyName("error_analysis")]
    public List<string> ErrorAnalysis { get; set; } = [];

    [JsonPropertyName("difficulty_level")]
    public string DifficultyLevel { get; set; } = "basic";

    [JsonPropertyName("suggestion")]
    public string Suggestion { get; set; } = string.Empty;

    public SentenceRatingResponse ToResponse()
    {
        return new SentenceRatingResponse(
            Math.Clamp(GrammarScore, 0, 5),
            Math.Clamp(NaturalScore, 0, 5),
            Math.Clamp(VocabularyScore, 0, 5),
            Math.Clamp(RelevanceScore, 0, 5),
            NormalizeGrade(OverallGrade),
            AiRevision,
            ErrorAnalysis,
            ParseDifficulty(DifficultyLevel),
            Suggestion);
    }

    private static string NormalizeGrade(string grade)
    {
        var value = grade.Trim().ToUpperInvariant();
        return value is "A" or "B" or "C" or "D" ? value : "C";
    }

    private static DifficultyLevel ParseDifficulty(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "advanced" => NextWord.Domain.Enums.DifficultyLevel.Advanced,
            "intermediate" => NextWord.Domain.Enums.DifficultyLevel.Intermediate,
            _ => NextWord.Domain.Enums.DifficultyLevel.Basic
        };
    }
}
