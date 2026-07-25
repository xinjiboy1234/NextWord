using System.Text.Json.Serialization;

namespace NextWord.Domain.Services;

/// <summary>InsightAgent LLM 返回 JSON 的反序列化载体（T-007）。</summary>
public sealed class BottleneckInsightJson
{
    [JsonPropertyName("nature")]
    public string Nature { get; set; } = string.Empty;

    [JsonPropertyName("statement")]
    public string Statement { get; set; } = string.Empty;

    [JsonPropertyName("evidenceLogIds")]
    public List<string> EvidenceLogIds { get; set; } = [];
}
