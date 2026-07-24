using System.Text.Json.Serialization;

namespace NextWord.Domain.Services;

internal sealed class ScenarioAnnotationJson
{
    [JsonPropertyName("annotations")]
    public List<ScenarioAnnotationItemJson> Annotations { get; set; } = [];
}

internal sealed class ScenarioAnnotationItemJson
{
    [JsonPropertyName("lemma")]
    public string Lemma { get; set; } = string.Empty;

    [JsonPropertyName("scenarios")]
    public List<string> Scenarios { get; set; } = [];

    [JsonPropertyName("utility")]
    public string Utility { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;
}
