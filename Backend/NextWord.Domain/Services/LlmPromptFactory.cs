using NextWord.Domain.Models;

namespace NextWord.Domain.Services;

public static class LlmPromptFactory
{
    public static string BuildDifficultyPrompt(ItemRatingRequest request)
    {
        return $"Rate the {request.ItemType} difficulty for: {request.Text}";
    }

    public static string BuildSentenceRatingPrompt(SentenceRatingRequest request)
    {
        return $$"""
        You are an English language assessment assistant. Rate this sentence.

        User Level: {{request.UserLevel}}
        Target Word: {{request.TargetWord}}
        Scene: {{request.Scene}}
        User Sentence: {{request.UserSentence}}

        Return only JSON:
        {
          "grammar_score": 0,
          "natural_score": 0,
          "vocabulary_score": 0,
          "relevance_score": 0,
          "overall_grade": "A/B/C/D",
          "ai_revision": "string",
          "error_analysis": ["string"],
          "difficulty_level": "basic|intermediate|advanced",
          "suggestion": "string"
        }

        Rules:
        - Scores must be integers from 0 to 5.
        - Be fair but not overly generous.
        - Evaluate whether the target word is used naturally and correctly.
        - If this is free expression, evaluate the whole passage.
        """;
    }
}
