using Microsoft.Extensions.Caching.Distributed;
using NextWord.Domain.Interfaces;
using System.Text.Json;

namespace NextWord.Infrastructure.Caching;

/// <summary>
/// 生产环境 Redis 缓存。通过 IDistributedCache（StackExchange.Redis）序列化存储。
/// </summary>
public sealed class RedisCacheService(IDistributedCache cache) : ICacheService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var bytes = await cache.GetAsync(key, cancellationToken);
        if (bytes is null || bytes.Length == 0)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(bytes, SerializerOptions);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, SerializerOptions);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration,
        };
        return cache.SetAsync(key, bytes, options, cancellationToken);
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        => cache.RemoveAsync(key, cancellationToken);
}
