using System.Text.Json.Serialization;

namespace NextWord.Domain.Services;

/// <summary>Profiler LLM 返回 JSON 的反序列化载体（T-005）。</summary>
public sealed class WeaknessProfileJson
{
    [JsonPropertyName("findings")]
    public List<WeaknessProfileFindingJson> Findings { get; set; } = [];
}

public sealed class WeaknessProfileFindingJson
{
    [JsonPropertyName("dimension")]
    public string Dimension { get; set; } = string.Empty;

    [JsonPropertyName("dimensionKey")]
    public string DimensionKey { get; set; } = string.Empty;

    [JsonPropertyName("polarity")]
    public string Polarity { get; set; } = string.Empty;

    [JsonPropertyName("statement")]
    public string Statement { get; set; } = string.Empty;

    [JsonPropertyName("evidence")]
    public List<WeaknessProfileEvidenceJson> Evidence { get; set; } = [];

    [JsonPropertyName("confidence")]
    public string Confidence { get; set; } = string.Empty;
}

public sealed class WeaknessProfileEvidenceJson
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("refId")]
    public string RefId { get; set; } = string.Empty;

    [JsonPropertyName("metric")]
    public string? Metric { get; set; }

    [JsonPropertyName("op")]
    public string? Op { get; set; }

    [JsonPropertyName("value")]
    public double? Value { get; set; }
}
