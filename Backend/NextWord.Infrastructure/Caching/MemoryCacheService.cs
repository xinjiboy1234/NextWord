using Microsoft.Extensions.Caching.Memory;
using NextWord.Domain.Interfaces;

namespace NextWord.Infrastructure.Caching;

/// <summary>
/// 开发环境缓存实现。生产可切换为 RedisCacheService。
/// </summary>
public sealed class MemoryCacheService(IMemoryCache cache) : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        cache.TryGetValue(key, out T? value);
        return Task.FromResult(value);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        cache.Set(key, value, expiration);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        cache.Remove(key);
        return Task.CompletedTask;
    }
}
