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

    public static string BuildVocabExtractPrompt(VocabExtractRequest request)
    {
        return $$"""
        You are a vocabulary extraction assistant for English learners.

        Article Level: {{request.ArticleLevel}}
        User Level: {{request.UserLevel}}

        Article Title: {{request.ArticleTitle}}

        Article:
        {{request.ArticleContent}}

        Extract vocabulary worth learning for a {{request.UserLevel}} student.
        Return only JSON:
        {
          "keyVocab": [
            {
              "word": "string",
              "contextMeaning": "string",
              "specialUsage": "string",
              "difficulty": "basic|intermediate|advanced",
              "action": "learn_now|review_later|challenge_only"
            }
          ],
          "skippedBasic": ["string"],
          "skippedRare": ["string"]
        }

        Rules:
        - Max 10 key vocabulary items.
        - Evaluate each word IN CONTEXT.
        - Do not include very basic words.
        """;
    }

    public static string BuildCommentReplyPrompt(CommentReplyRequest request)
    {
        return $$"""
        You are a reading tutor. Reply helpfully to the learner's paragraph comment.

        Article: {{request.ArticleTitle}}
        Paragraph: {{request.ParagraphText}}
        Learner Comment: {{request.CommentText}}

        Return plain text only, 2-4 sentences, encouraging and explanatory.
        """;
    }
}
