namespace NextWord.Domain.Entities;

/// <summary>
/// Word ↔ 子场景多对多关联（设计方案 §3）：每词 0–3 个子场景，0 个 = core 通用桶。
/// </summary>
public sealed class WordScenario
{
    public Guid WordId { get; set; }
    public string ScenarioKey { get; set; } = string.Empty;

    public Word? Word { get; set; }
}
