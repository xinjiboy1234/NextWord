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
        var explanationLanguage = ExplanationLanguageHelper.Resolve(
            request.ExplanationLanguage,
            ExplanationLanguageHelper.Default);
        var explanationLanguageName = ExplanationLanguageHelper.GetPromptDisplayName(explanationLanguage);

        return $$"""
        You are an English language assessment assistant. Rate this sentence.

        User Level: {{request.UserLevel}}
        Target Word: {{request.TargetWord}}
        Scene: {{request.Scene}}
        User Sentence: {{request.UserSentence}}
        Feedback Language: {{explanationLanguage}} ({{explanationLanguageName}})

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
        - Write error_analysis and suggestion in {{explanationLanguageName}}.
        - Keep ai_revision in natural English as the corrected learner sentence.
        """;
    }

    public static string BuildDefinitionPrompt(DefinitionRequest request)
    {
        var explanationLanguage = ExplanationLanguageHelper.Resolve(
            request.ExplanationLanguage,
            ExplanationLanguageHelper.Default);
        var explanationLanguageName = ExplanationLanguageHelper.GetPromptDisplayName(explanationLanguage);
        var context = string.IsNullOrWhiteSpace(request.Context) ? "(no sentence provided)" : request.Context;

        return $$"""
        You are a vocabulary assistant for English learners. Explain a word in reading context.

        Word: {{request.Word}}
        Context Sentence: {{context}}
        Feedback Language: {{explanationLanguage}} ({{explanationLanguageName}})

        Return only JSON:
        {
          "phonetics": "string",
          "meanings": [
            {
              "definition": "string",
              "is_contextual": true
            }
          ],
          "collocations": ["string"],
          "examples": [
            {
              "kind": "contextual",
              "sentence": "English sentence tied to the context",
              "explanation": "essence note in {{explanationLanguageName}}"
            },
            {
              "kind": "general",
              "sentence": "English sentence from another scenario",
              "explanation": "essence note in {{explanationLanguageName}}"
            }
          ],
          "special_usage": "string",
          "difficulty_level": "basic|intermediate|advanced",
          "cefr_level": "A1|A2|B1|B2|C1|C2"
        }

        Rules:
        - meanings[0] must explain how the word is used in the given context sentence.
        - Write definition, special_usage, collocation glosses, and example explanations in {{explanationLanguageName}}.
        - Keep example sentences in natural English.
        - examples[0] (contextual) must reflect usage in the given context sentence.
        - examples[1] (general) should come from a different everyday scenario; omit if the word is too rare, too specialized, or not worth illustrating at this level.
        - Return 0-2 examples. Be concise: one primary contextual meaning, up to 2 collocations.
        """;
    }

    public static string BuildVocabExtractPrompt(VocabExtractRequest request)
    {
        var explanationLanguage = ExplanationLanguageHelper.Resolve(
            request.ExplanationLanguage,
            ExplanationLanguageHelper.Default);
        var explanationLanguageName = ExplanationLanguageHelper.GetPromptDisplayName(explanationLanguage);

        return $$"""
        You are a vocabulary extraction assistant for English learners.

        Article Level: {{request.ArticleLevel}}
        User Level: {{request.UserLevel}}
        Feedback Language: {{explanationLanguage}} ({{explanationLanguageName}})

        Article Title: {{request.ArticleTitle}}

        Article:
        {{request.ArticleContent}}

        Extract vocabulary worth learning for a {{request.UserLevel}} student.
        Return only JSON:
        {
          "keyVocab": [
            {
              "word": "string",
              "phonetics": "string",
              "contextMeaning": "string",
              "usageExample": {
                "sentence": "English sentence from this article's context",
                "explanation": "essence note in {{explanationLanguageName}}"
              },
              "generalExample": {
                "sentence": "English sentence from another scenario",
                "explanation": "essence note in {{explanationLanguageName}}"
              },
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
        - Keep word as the English lemma.
        - Write contextMeaning and example explanations in {{explanationLanguageName}}.
        - phonetics is required for common words (IPA).
        - usageExample must show how the word is used in THIS article; generalExample is optional for another scenario.
        - Omit generalExample if the word is too specialized or not worth a second example at this level.
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

    public static string BuildScenarioAnnotationPrompt(ScenarioAnnotationRequest request)
    {
        var scenarioList = string.Join("\n", Scenarios.ScenarioTaxonomy.All.Select(
            item => $"- {item.Key} ({item.ZhName}, 大类 {item.CategoryKey})"));
        var wordList = string.Join("\n", request.Words.Select(
            item => $"- {item.Lemma} ({item.PartOfSpeech}) {string.Join("；", item.Meanings)}"));

        return $$"""
        You annotate English vocabulary for a life-expression learning app.

        Sub-scenarios (pick 0-3 per word; pick 0 only for cross-scenario core words like be/have/get or connectors):
        {{scenarioList}}

        Fields per word:
        - scenarios: 0-3 sub-scenario keys where the word is most useful for EXPRESSION.
        - utility: high|medium|low — everyday spoken frequency × irreplaceability in expression. Use low for rare/bookish words.
        - role: core_verb|connector|scene_noun|phrase_pattern — the word's role in spoken expression.

        Words:
        {{wordList}}

        Return only JSON:
        {
          "annotations": [
            { "lemma": "string", "scenarios": ["key"], "utility": "high", "role": "core_verb" }
          ]
        }

        Rules:
        - Return exactly one annotation per input word, keeping the input lemma unchanged.
        - scenarios must only use keys from the list above.
        """;
    }
}
