using System.Text.Json.Serialization;

namespace NextWord.Domain.Services;

internal sealed class VocabExtractJson
{
    [JsonPropertyName("keyVocab")]
    public List<KeyVocabJson> KeyVocab { get; set; } = [];

    [JsonPropertyName("skippedBasic")]
    public List<string> SkippedBasic { get; set; } = [];

    [JsonPropertyName("skippedRare")]
    public List<string> SkippedRare { get; set; } = [];
}

internal sealed class KeyVocabJson
{
    [JsonPropertyName("word")]
    public string Word { get; set; } = string.Empty;

    [JsonPropertyName("phonetics")]
    public string Phonetics { get; set; } = string.Empty;

    [JsonPropertyName("contextMeaning")]
    public string ContextMeaning { get; set; } = string.Empty;

    [JsonPropertyName("usageExample")]
    public WordExampleJsonDto? UsageExample { get; set; }

    [JsonPropertyName("generalExample")]
    public WordExampleJsonDto? GeneralExample { get; set; }

    [JsonPropertyName("difficulty")]
    public string Difficulty { get; set; } = "basic";

    [JsonPropertyName("action")]
    public string Action { get; set; } = "learn_now";
}
