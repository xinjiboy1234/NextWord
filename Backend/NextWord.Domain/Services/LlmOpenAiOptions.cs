namespace NextWord.Domain.Services;

public sealed class LlmOpenAiOptions
{
    public bool Enabled { get; set; }
    public string Model { get; set; } = "gpt-4o-mini";
    public string? ApiKey { get; set; }
    public string ApiKeyEnvironmentVariable { get; set; } = "OPENAI_API_KEY";

    /// <summary>可选自定义端点（OpenAI 兼容服务，如 DashScope compatible-mode）；空 = 官方 OpenAI。</summary>
    public string? BaseUrl { get; set; }
}
