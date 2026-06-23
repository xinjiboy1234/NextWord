using NextWord.Domain.Enums;

namespace NextWord.Domain.Entities;

public sealed class UserLlmSettings
{
    public Guid UserId { get; set; }
    public LlmProviderType Provider { get; set; } = LlmProviderType.OpenAI;
    public string BaseUrl { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o-mini";
    public string? ApiKey { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User User { get; set; } = null!;
}
