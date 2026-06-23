using NextWord.Domain.Enums;

namespace NextWord.Domain.Models;

public sealed record LlmPresetInfo(
    string Id,
    string Name,
    LlmProviderType Provider,
    string BaseUrl,
    string DefaultModel);

public static class LlmPresets
{
    public static readonly IReadOnlyList<LlmPresetInfo> All =
    [
        new("openai", "OpenAI", LlmProviderType.OpenAI, "https://api.openai.com/v1", "gpt-4o-mini"),
        new("deepseek", "DeepSeek", LlmProviderType.DeepSeek, "https://api.deepseek.com", "deepseek-chat"),
        new("qwen", "Qwen", LlmProviderType.Qwen, "https://dashscope.aliyuncs.com/compatible-mode/v1", "qwen-plus")
    ];

    public static LlmPresetInfo? FindById(string? presetId)
        => All.FirstOrDefault(item => string.Equals(item.Id, presetId, StringComparison.OrdinalIgnoreCase));
}
