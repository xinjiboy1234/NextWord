using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Domain.Services;

namespace NextWord.UnitTests;

public sealed class ReadingPromptTests
{
    [Fact]
    public void BuildDefinitionPrompt_UsesConfiguredChineseFeedbackLanguage()
    {
        var prompt = LlmPromptFactory.BuildDefinitionPrompt(new DefinitionRequest(
            "cart",
            "Sometimes we buy ice cream from a cart.",
            ExplanationLanguage: "zh-CN"));

        Assert.Contains("Feedback Language: zh-CN (Chinese (Simplified))", prompt);
        Assert.Contains("Write definition, special_usage, collocation glosses, and example explanations in Chinese (Simplified).", prompt);
        Assert.Contains("\"examples\":", prompt);
        Assert.Contains("Word: cart", prompt);
    }

    [Fact]
    public void BuildVocabExtractPrompt_UsesConfiguredChineseForContextFields()
    {
        var prompt = LlmPromptFactory.BuildVocabExtractPrompt(new VocabExtractRequest(
            "A Day at the Park",
            "Sometimes we buy ice cream from a cart.",
            "A1",
            "A2",
            ExplanationLanguage: "zh-CN"));

        Assert.Contains("Feedback Language: zh-CN (Chinese (Simplified))", prompt);
        Assert.Contains("Write contextMeaning and example explanations in Chinese (Simplified).", prompt);
        Assert.Contains("\"phonetics\":", prompt);
        Assert.Contains("Keep word as the English lemma.", prompt);
    }

    [Fact]
    public void ParseDefinition_DeserializesStructuredExamples()
    {
        var json = """
            {
              "phonetics": "/kɑːrt/",
              "meanings": [
                {
                  "definition": "流动售货车",
                  "is_contextual": true
                }
              ],
              "collocations": ["ice cream cart"],
              "examples": [
                {
                  "kind": "contextual",
                  "sentence": "We bought snacks from a cart.",
                  "explanation": "贴合文中售物场景"
                },
                {
                  "kind": "general",
                  "sentence": "She pushed the cart down the aisle.",
                  "explanation": "日常购物场景"
                }
              ],
              "special_usage": "常与 from 连用",
              "difficulty_level": "basic",
              "cefr_level": "A2"
            }
            """;

        var response = LlmResponseParser.ParseDefinition(json, "cart", "buy ice cream from a cart");

        Assert.Equal("cart", response.Word);
        Assert.Equal("流动售货车", response.Meanings[0].Definition);
        Assert.Equal(2, response.Examples.Count);
        Assert.Equal(WordExampleKind.Contextual, response.Examples[0].Kind);
        Assert.Equal("常与 from 连用", response.SpecialUsage);
    }

    [Fact]
    public void ParseDefinition_AllowsEmptyExamples()
    {
        var json = """
            {
              "phonetics": "/rɛr/",
              "meanings": [{ "definition": "罕见的", "is_contextual": true }],
              "collocations": [],
              "examples": [],
              "special_usage": "",
              "difficulty_level": "advanced",
              "cefr_level": "C1"
            }
            """;

        var response = LlmResponseParser.ParseDefinition(json, "rare", "a rare term");

        Assert.Empty(response.Examples);
    }

    [Fact]
    public async Task MockGetDefinitionAsync_DoesNotReturnTemplatePlaceholder()
    {
        var provider = new LlmMockProvider(new FixedModelProfileResolver());
        var response = await provider.GetDefinitionAsync(
            new DefinitionRequest("cart", "Sometimes we buy ice cream from a cart.", ExplanationLanguage: "zh-CN"),
            CancellationToken.None);

        var definition = response.Meanings[0].Definition;
        Assert.DoesNotContain("的常见中文含义", definition);
        Assert.Contains("手推车", definition);
        Assert.Equal(2, response.Examples.Count);
    }

    [Fact]
    public async Task MockExtractVocabAsync_ReturnsPhoneticsAndUsageExamples()
    {
        var provider = new LlmMockProvider(new FixedModelProfileResolver());
        var response = await provider.ExtractVocabAsync(
            new VocabExtractRequest(
                "A Day at the Park",
                "Sometimes we buy ice cream from a cart near the park.",
                "A1",
                "A2",
                ExplanationLanguage: "zh-CN"),
            CancellationToken.None);

        Assert.NotEmpty(response.KeyVocab);
        Assert.Contains("在本文中指", response.KeyVocab[0].ContextMeaning);
        Assert.False(string.IsNullOrWhiteSpace(response.KeyVocab[0].Phonetics));
        Assert.NotNull(response.KeyVocab[0].UsageExample);
    }

    private sealed class FixedModelProfileResolver : IModelProfileResolver
    {
        public ModelProfile Resolve(string? profileId) => new() { Id = profileId ?? "local-dev" };
    }
}
