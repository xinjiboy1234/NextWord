namespace NextWord.Domain.Services;

public sealed class LlmOpenAiOptions
{
    public bool Enabled { get; set; }
    public string Model { get; set; } = "gpt-4o-mini";
    public string? ApiKey { get; set; }
    public string ApiKeyEnvironmentVariable { get; set; } = "OPENAI_API_KEY";
}
