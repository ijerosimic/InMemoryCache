using Microsoft.Extensions.Options;

namespace InMemoryCache;

public interface IInMemoryCache<T>
{
    /// <summary>
    /// Tries to get the item under the given cache key.
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <returns>Result object containing cache value and a flag indicating whether value has value or is null.</returns>
    /// <exception cref="ArgumentNullException">Key is null or white space.</exception>
    InMemoryCacheValue<T> Get(string key);

    /// <summary>
    /// Adds an item to the cache.
    /// If adding an item would cause the cache item count to go above the configured threshold, it will trigger a LRU eviction.
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="value">Cache value</param>
    /// <param name="evictedKey">Cache key of evicted item. Null if no key was evicted.</param>
    /// <exception cref="ArgumentNullException"></exception>
    void Set(string key, T value, out string? evictedKey);

    /// <summary>
    /// Clears the cache.
    /// </summary>
    void Flush();
    int GetCount();
    int GetThreshold();
}

public class InMemoryCache<T> : IInMemoryCache<T>
{
    private readonly LinkedList<string> _keysQueue;
    private readonly Dictionary<string, T> _cache;
    private readonly object _obj = new();
    private readonly int _maxItemsCount;

    public InMemoryCache(IOptions<InMemoryCacheOptions> cacheOptions)
    {
        var configuredMax = cacheOptions.Value?.MaxItems;

        _maxItemsCount = configuredMax is >= 1
            ? configuredMax.Value
            : throw new ArgumentOutOfRangeException(nameof(InMemoryCacheOptions.MaxItems),
                "Invalid limit for max cache storage. Must be >= 1.");

        _keysQueue = new LinkedList<string>();
        _cache = new Dictionary<string, T>();
    }

    public int GetThreshold() => _maxItemsCount;

    public int GetCount()
    {
        lock (_obj)
            return _cache.Count;
    }

    public InMemoryCacheValue<T> Get(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key), "Cache key must have a value.");

        lock (_obj)
        {
            if (!_cache.ContainsKey(key))
                return new InMemoryCacheValue<T>
                {
                    Value = default,
                    HasValue = false
                };

            var node = _cache[key];
            _keysQueue.Remove(key);
            _keysQueue.AddFirst(key);

            return new InMemoryCacheValue<T>
            {
                Value = node,
                HasValue = true
            };
        }
    }

    public void Set(string key, T value, out string? evictedKey)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key), "Cache key must have a value.");

        if (value is null)
            throw new ArgumentNullException(nameof(value), "Cannot cache null value.");

        lock (_obj)
        {
            evictedKey = null;

            if (_cache.ContainsKey(key))
            {
                _cache[key] = value;
                _keysQueue.Remove(key);
                _keysQueue.AddFirst(key);

                return;
            }

            if (_cache.Count >= _maxItemsCount)
            {
                var last = _keysQueue.Last;
                _cache.Remove(last!.Value);
                _keysQueue.RemoveLast();

                evictedKey = last.Value;
            }

            _cache[key] = value;
            _keysQueue.AddFirst(key);
        }
    }

    public void Flush()
    {
        lock (_obj)
        {
            _cache.Clear();
            _keysQueue.Clear();
        }
    }
}

/// <summary>
/// Cache value. Get result.
/// </summary>
public class InMemoryCacheValue<T>
{
    public T? Value { get; set; }

    /// <summary>
    /// Indicates whether value has value.
    /// </summary>
    /// <value><c>true</c> if has value; otherwise, <c>false</c>.</value>
    public bool HasValue { get; set; }
}

public class InMemoryCacheOptions
{
    public int MaxItems { get; set; }
}