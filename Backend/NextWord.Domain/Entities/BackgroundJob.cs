namespace NextWord.Domain.Entities;

public sealed class BackgroundJob
{
    public long Id { get; set; }
    public string JobType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public string Status { get; set; } = "Pending";
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>T-013：进入 Processing 的时刻，僵尸任务回收据此判超时。</summary>
    public DateTimeOffset? StartedAt { get; set; }
    /// <summary>T-013：僵尸回收重试次数，超上限标记 Failed 留痕。</summary>
    public int RetryCount { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
