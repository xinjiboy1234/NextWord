using NextWord.Domain.Models;
using NextWord.Domain.Services;

namespace NextWord.UnitTests;

public sealed class SentenceRatingPromptTests
{
    [Fact]
    public void BuildSentenceRatingPrompt_UsesConfiguredChineseFeedbackLanguage()
    {
        var prompt = LlmPromptFactory.BuildSentenceRatingPrompt(new SentenceRatingRequest(
            "I go school.",
            "school",
            "life",
            "A2",
            ExplanationLanguage: "zh-CN"));

        Assert.Contains("Feedback Language: zh-CN (Chinese (Simplified))", prompt);
        Assert.Contains("Write error_analysis and suggestion in Chinese (Simplified).", prompt);
        Assert.Contains("Keep ai_revision in natural English", prompt);
    }

    [Fact]
    public void ExplanationLanguageHelper_Resolve_PrefersRequestOverDefault()
    {
        var resolved = ExplanationLanguageHelper.Resolve("en-US", "zh-CN");

        Assert.Equal("en-US", resolved);
    }
}
