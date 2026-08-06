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
    public void BuildSentenceRatingPrompt_MapsSceneKeyToChineseName()
    {
        var prompt = LlmPromptFactory.BuildSentenceRatingPrompt(new SentenceRatingRequest(
            "I ask for directions.",
            "directions",
            "directions",
            "B1"));

        // T-030：prompt 用场景中文名，避免反馈文案复述内部 key（directions → 问路导航，与计划卡口径一致）
        Assert.Contains("Scene: 问路导航", prompt);
        Assert.DoesNotContain("Scene: directions", prompt);
    }

    [Fact]
    public void BuildSentenceRatingPrompt_KeepsUnknownSceneKeyAsFallback()
    {
        var prompt = LlmPromptFactory.BuildSentenceRatingPrompt(new SentenceRatingRequest(
            "I write whatever.", "whatever", "free", "B1"));

        Assert.Contains("Scene: free", prompt);
    }

    [Fact]
    public void ExplanationLanguageHelper_Resolve_PrefersRequestOverDefault()
    {
        var resolved = ExplanationLanguageHelper.Resolve("en-US", "zh-CN");

        Assert.Equal("en-US", resolved);
    }
}
