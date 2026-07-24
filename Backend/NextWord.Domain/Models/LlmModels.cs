using NextWord.Domain.Enums;

namespace NextWord.Domain.Models;

public sealed record LlmRequestOptions(
    string ModelProfileId = "local-dev",
    string Purpose = "difficulty_rating");

public sealed record ItemRatingRequest(
    ItemType ItemType,
    string Text,
    LlmRequestOptions? Options = null);

public sealed record DifficultyRating(
    ItemType ItemType,
    DifficultyLevel DifficultyLevel,
    CefrLevel CefrLevel,
    string Reason,
    RecommendedAction RecommendedAction,
    double Confidence,
    string ModelProfileId);

public sealed record DefinitionRequest(
    string Word,
    string? Context = null,
    LlmRequestOptions? Options = null,
    string? ExplanationLanguage = null);

public enum WordExampleKind
{
    Contextual,
    General
}

public sealed record WordExample(
    WordExampleKind Kind,
    string Sentence,
    string Explanation);

public sealed record DefinitionResponse(
    string Word,
    string Phonetics,
    IReadOnlyList<Meaning> Meanings,
    IReadOnlyList<string> Collocations,
    IReadOnlyList<WordExample> Examples,
    string SpecialUsage,
    DifficultyLevel DifficultyLevel,
    CefrLevel CefrLevel);

public sealed record Meaning(
    string Definition,
    bool IsContextual,
    string Context);

public sealed record SentenceRatingRequest(
    string UserSentence,
    string TargetWord,
    string Scene,
    string UserLevel,
    LlmRequestOptions? Options = null,
    string? ExplanationLanguage = null);

public sealed record SentenceRatingResponse(
    int GrammarScore,
    int NaturalScore,
    int VocabularyScore,
    int RelevanceScore,
    string OverallGrade,
    string AiRevision,
    IReadOnlyList<string> ErrorAnalysis,
    DifficultyLevel DifficultyLevel,
    string Suggestion);

public sealed record VocabExtractRequest(
    string ArticleTitle,
    string ArticleContent,
    string ArticleLevel,
    string UserLevel,
    LlmRequestOptions? Options = null,
    string? ExplanationLanguage = null);

public sealed record KeyVocabItem(
    string Word,
    string Phonetics,
    string ContextMeaning,
    WordExample? UsageExample,
    WordExample? GeneralExample,
    DifficultyLevel Difficulty,
    RecommendedAction Action);

public sealed record ArticleWordDetailResult(
    DefinitionResponse Definition,
    bool FromCache);

public sealed record VocabExtractResponse(
    IReadOnlyList<KeyVocabItem> KeyVocab,
    IReadOnlyList<string> SkippedBasic,
    IReadOnlyList<string> SkippedRare);

public sealed record CommentReplyRequest(
    string ParagraphText,
    string CommentText,
    string ArticleTitle,
    LlmRequestOptions? Options = null);

public sealed record CommentReplyResponse(string Reply);

public sealed record ScenarioAnnotationItem(
    string Lemma,
    string PartOfSpeech,
    IReadOnlyList<string> Meanings);

public sealed record ScenarioAnnotationRequest(
    IReadOnlyList<ScenarioAnnotationItem> Words,
    LlmRequestOptions? Options = null);

public sealed record ScenarioAnnotationResult(
    string Lemma,
    IReadOnlyList<string> ScenarioKeys,
    WordUtility Utility,
    ExpressionRole Role);

public sealed record ScenarioAnnotationResponse(
    IReadOnlyList<ScenarioAnnotationResult> Annotations);

public sealed record ReadingAgentRequest(
    string Intent,
    string ArticleTitle,
    string ArticleContent,
    string? SelectedWord,
    string? ParagraphText,
    string UserLevel,
    Guid? UserId = null,
    LlmRequestOptions? Options = null,
    string? ExplanationLanguage = null);

public sealed record ReadingAgentSkillCall(
    string SkillName,
    string Summary);

public sealed record ReadingAgentResponse(
    string Message,
    IReadOnlyList<ReadingAgentSkillCall> SkillCalls,
    DefinitionResponse? Definition,
    VocabExtractResponse? VocabExtract,
    CommentReplyResponse? CommentReply);

public sealed class ModelProfile
{
    public string Id { get; set; } = "local-dev";
    public string Provider { get; set; } = "Mock";
    public string Model { get; set; } = "mock-hardcoded-v1";
    public string? Endpoint { get; set; }
    public string? ApiKeyName { get; set; }
    public float? Temperature { get; set; }
    public int? MaxOutputTokens { get; set; }
    public int? TimeoutSeconds { get; set; }
    public bool EnableToolCalling { get; set; }
    public bool EnableStructuredOutput { get; set; } = true;
    public Dictionary<string, object?> ProviderOptions { get; set; } = [];
}
