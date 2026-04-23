using System.Collections.Concurrent;

namespace Core.Tui.Azure;

/// <summary>
/// Generic TTL-based cache with dogpile prevention.
/// Mirrors the TypeScript CacheService pattern.
/// </summary>
public class AzureCache<T>
{
    private readonly TimeSpan _ttl;
    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new();
    private readonly ConcurrentDictionary<string, Task<T>> _pending = new();

    public AzureCache(TimeSpan? ttl = null)
    {
        _ttl = ttl ?? TimeSpan.FromMinutes(5);
    }

    public async Task<T> GetOrFetchAsync(string key, Func<Task<T>> factory)
    {
        if (_entries.TryGetValue(key, out var entry) && !entry.IsExpired)
            return entry.Value;

        // Dogpile prevention: if another caller is already fetching, await that task
        var task = _pending.GetOrAdd(key, _ => FetchAndStoreAsync(key, factory));
        try
        {
            return await task;
        }
        finally
        {
            _pending.TryRemove(key, out _);
        }
    }

    private async Task<T> FetchAndStoreAsync(string key, Func<Task<T>> factory)
    {
        var value = await factory();
        _entries[key] = new CacheEntry(value, DateTime.UtcNow + _ttl);
        return value;
    }

    public void Invalidate(string key) => _entries.TryRemove(key, out _);

    public void Clear() => _entries.Clear();

    public IEnumerable<string> Keys => _entries.Keys;

    private record CacheEntry(T Value, DateTime ExpiresAt)
    {
        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    }
}
