using NextWord.Domain.Enums;
using NextWord.Domain.Models;
using NextWord.Domain.Scenarios;
using NextWord.Domain.Services;

namespace NextWord.UnitTests;

public sealed class ScenarioAnnotationParsingTests
{
    [Fact]
    public void ParseScenarioAnnotation_ParsesValidResponse()
    {
        const string content = """
        {
          "annotations": [
            { "lemma": "boil", "scenarios": ["home_cooking", "daily_routine"], "utility": "high", "role": "core_verb" },
            { "lemma": "however", "scenarios": [], "utility": "medium", "role": "connector" }
          ]
        }
        """;

        var result = LlmResponseParser.ParseScenarioAnnotation(content);

        Assert.Equal(2, result.Annotations.Count);
        var boil = result.Annotations[0];
        Assert.Equal("boil", boil.Lemma);
        Assert.Equal(["home_cooking", "daily_routine"], boil.ScenarioKeys);
        Assert.Equal(WordUtility.High, boil.Utility);
        Assert.Equal(ExpressionRole.CoreVerb, boil.Role);
        Assert.Empty(result.Annotations[1].ScenarioKeys);
    }

    [Fact]
    public void ParseScenarioAnnotation_ExtractsJsonFromProse()
    {
        const string content = "Here you go:\n{\"annotations\":[{\"lemma\":\"pan\",\"scenarios\":[\"home_cooking\"],\"utility\":\"high\",\"role\":\"scene_noun\"}]}\nDone.";

        var result = LlmResponseParser.ParseScenarioAnnotation(content);

        Assert.Single(result.Annotations);
        Assert.Equal("pan", result.Annotations[0].Lemma);
    }

    [Fact]
    public void ParseScenarioAnnotation_DropsUnknownScenarioKeysAndCapsAtThree()
    {
        const string content = """
        {"annotations":[{"lemma":"trip","scenarios":["travel_lodging","mars_base","transport","directions","shopping"],"utility":"medium","role":"scene_noun"}]}
        """;

        var result = LlmResponseParser.ParseScenarioAnnotation(content);

        var item = Assert.Single(result.Annotations);
        Assert.Equal(3, item.ScenarioKeys.Count);
        Assert.DoesNotContain("mars_base", item.ScenarioKeys);
    }

    [Fact]
    public void ParseScenarioAnnotation_DropsEntriesWithInvalidUtilityOrRole()
    {
        const string content = """
        {"annotations":[
          {"lemma":"good","scenarios":[],"utility":"awesome","role":"connector"},
          {"lemma":"book","scenarios":["study_talk"],"utility":"high","role":"wizard"},
          {"lemma":"pan","scenarios":["home_cooking"],"utility":"high","role":"scene_noun"}
        ]}
        """;

        var result = LlmResponseParser.ParseScenarioAnnotation(content);

        var item = Assert.Single(result.Annotations);
        Assert.Equal("pan", item.Lemma);
    }

    [Fact]
    public void BuildScenarioAnnotationPrompt_ContainsAllSubScenarioKeys()
    {
        var request = new ScenarioAnnotationRequest([new ScenarioAnnotationItem("boil", "v.", ["煮沸"])]);

        var prompt = LlmPromptFactory.BuildScenarioAnnotationPrompt(request);

        foreach (var item in ScenarioTaxonomy.All)
        {
            Assert.Contains(item.Key, prompt);
        }

        Assert.Contains("boil", prompt);
    }

    [Fact]
    public async Task MockProvider_InfersRoleFromPartOfSpeech()
    {
        var provider = new LlmMockProvider(new ModelProfileResolver());
        var request = new ScenarioAnnotationRequest(
        [
            new ScenarioAnnotationItem("boil", "v.", ["煮沸"]),
            new ScenarioAnnotationItem("however", "conj.", ["然而"]),
            new ScenarioAnnotationItem("pick up", "phr.", ["捡起"]),
            new ScenarioAnnotationItem("pan", "n.", ["平底锅"])
        ]);

        var result = await provider.AnnotateScenarioAsync(request, CancellationToken.None);

        Assert.Equal(4, result.Annotations.Count);
        Assert.Equal(ExpressionRole.CoreVerb, result.Annotations[0].Role);
        Assert.Equal(ExpressionRole.Connector, result.Annotations[1].Role);
        Assert.Equal(ExpressionRole.PhrasePattern, result.Annotations[2].Role);
        Assert.Equal(ExpressionRole.SceneNoun, result.Annotations[3].Role);
        Assert.All(result.Annotations, item => Assert.Empty(item.ScenarioKeys));
        Assert.All(result.Annotations, item => Assert.Equal(WordUtility.Medium, item.Utility));
    }
}
