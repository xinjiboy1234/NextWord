using System.Text.Json.Serialization;
using NextWord.Domain.Enums;

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

    [JsonPropertyName("contextMeaning")]
    public string ContextMeaning { get; set; } = string.Empty;

    [JsonPropertyName("specialUsage")]
    public string SpecialUsage { get; set; } = string.Empty;

    [JsonPropertyName("difficulty")]
    public string Difficulty { get; set; } = "basic";

    [JsonPropertyName("action")]
    public string Action { get; set; } = "learn_now";
}
