using System;
using System.Threading.Tasks;
using Jellyfin.Plugin.CnNfo.Configuration;
using Microsoft.Extensions.Caching.Memory;

namespace Jellyfin.Plugin.CnNfo.Cache;

public class CnNfoCache
{
    private readonly IMemoryCache _cache;

    public CnNfoCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    private PluginConfiguration Config => Plugin.Instance.Configuration;

    public bool TryGet<T>(string key, out T? value)
    {
        if (_cache.TryGetValue(key, out var raw) && raw is T t)
        {
            value = t;
            return true;
        }
        value = default;
        return false;
    }

    public void Set<T>(string key, T value, TimeSpan? ttl = null)
    {
        var minutes = Config.CacheMinutes <= 0 ? 60 : Config.CacheMinutes;
        var expiry = ttl ?? TimeSpan.FromMinutes(minutes);
        _cache.Set(key, value!, expiry);
    }

    public async Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl = null)
    {
        if (TryGet<T>(key, out var cached) && cached is not null)
        {
            return cached;
        }

        var value = await factory().ConfigureAwait(false);
        if (value is not null)
        {
            Set(key, value, ttl);
        }
        return value;
    }
}
