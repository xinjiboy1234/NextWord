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

    public static DefinitionResponse ParseDefinition(string content, string word, string? context)
    {
        var json = ExtractJson(content);
        var parsed = JsonSerializer.Deserialize<DefinitionJson>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("LLM returned empty word definition.");
        return parsed.ToResponse(word, context);
    }

    public static VocabExtractResponse ParseVocabExtract(string content)
    {
        var json = ExtractJson(content);
        var parsed = JsonSerializer.Deserialize<VocabExtractJson>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("LLM returned empty vocab extraction.");
        return new VocabExtractResponse(
            parsed.KeyVocab.Select(item => new KeyVocabItem(
                item.Word,
                item.Phonetics,
                item.ContextMeaning,
                MapOptionalExample(item.UsageExample, WordExampleKind.Contextual),
                MapOptionalExample(item.GeneralExample, WordExampleKind.General),
                ParseDifficulty(item.Difficulty),
                ParseAction(item.Action))).ToList(),
            parsed.SkippedBasic,
            parsed.SkippedRare);
    }

    /// <summary>
    /// 解析场景标注结果：无效场景 key 丢弃、场景数截断到 3 个、utility/role 无法识别时丢弃该条（由调用方决定重试）。
    /// </summary>
    public static ScenarioAnnotationResponse ParseScenarioAnnotation(string content)
    {
        var json = ExtractJson(content);
        var parsed = JsonSerializer.Deserialize<ScenarioAnnotationJson>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("LLM returned empty scenario annotation.");

        var results = new List<ScenarioAnnotationResult>();
        foreach (var item in parsed.Annotations)
        {
            if (string.IsNullOrWhiteSpace(item.Lemma)
                || !TryParseUtility(item.Utility, out var utility)
                || !TryParseRole(item.Role, out var role))
            {
                continue;
            }

            var scenarioKeys = item.Scenarios
                .Where(Scenarios.ScenarioTaxonomy.IsSubScenarioKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToList();

            results.Add(new ScenarioAnnotationResult(item.Lemma.Trim().ToLowerInvariant(), scenarioKeys, utility, role));
        }

        return new ScenarioAnnotationResponse(results);
    }

    private static bool TryParseUtility(string value, out WordUtility utility)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "high": utility = WordUtility.High; return true;
            case "medium": utility = WordUtility.Medium; return true;
            case "low": utility = WordUtility.Low; return true;
            default: utility = default; return false;
        }
    }

    private static bool TryParseRole(string value, out ExpressionRole role)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "core_verb": role = ExpressionRole.CoreVerb; return true;
            case "connector": role = ExpressionRole.Connector; return true;
            case "scene_noun": role = ExpressionRole.SceneNoun; return true;
            case "phrase_pattern": role = ExpressionRole.PhrasePattern; return true;
            default: role = default; return false;
        }
    }

    private static WordExample? MapOptionalExample(WordExampleJsonDto? item, WordExampleKind kind)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.Sentence))
        {
            return null;
        }

        return new WordExample(
            kind,
            item.Sentence.Trim(),
            string.IsNullOrWhiteSpace(item.Explanation) ? item.Sentence.Trim() : item.Explanation.Trim());
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
