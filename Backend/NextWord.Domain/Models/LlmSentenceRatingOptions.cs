namespace NextWord.Domain.Models;

public sealed class LlmSentenceRatingOptions
{
    public const string SectionName = "Llm:SentenceRating";

    /// <summary>
    /// BCP 47 locale for learner-facing feedback (error_analysis, suggestion).
    /// Default zh-CN; later can be overridden per user profile.
    /// </summary>
    public string ExplanationLanguage { get; set; } = "zh-CN";
}
