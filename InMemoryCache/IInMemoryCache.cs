using Microsoft.Extensions.Options;

namespace InMemoryCache;

public interface IInMemoryCache<T> where T : class
{
    T? Get(string key);
    void Set(string key, T value, out string? evictedValue);
    int GetCount();
    int GetThreshold();
}

public class InMemoryCache<T> : IInMemoryCache<T> where T : class
{
    private readonly LinkedList<string> _keysQueue;
    private readonly Dictionary<string, LinkedListNode<(string key, T? value)>> _cache;
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
        _cache = new Dictionary<string, LinkedListNode<(string, T?)>>();
    }

    public int GetThreshold() => _maxItemsCount;

    public int GetCount()
    {
        lock (_obj)
            return _cache.Count;
    }

    public T? Get(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key), "Cache key must have a value.");

        lock (_obj)
        {
            if (!_cache.ContainsKey(key))
                return null;

            var node = _cache[key];
            _keysQueue.Remove(node.Value.key);
            _keysQueue.AddFirst(node.Value.key);
            
            return node.Value.value;
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
                var cachedNode = _cache[key];
                _keysQueue.Remove(cachedNode.Value.key);
                _keysQueue.AddFirst(cachedNode.Value.key);
                _cache[key] = cachedNode;
            }

            var node = value;
            if (_cache.Count >= _maxItemsCount)
            {
                var last = _keysQueue.Last;
                _cache.Remove(last!.Value);
                _keysQueue.RemoveLast();
                evictedKey = last.Value;
            }

            _cache[key] = new LinkedListNode<(string key, T? value)>((key, value));
            _keysQueue.AddFirst(key);
        }
    }
}

public class InMemoryCacheOptions
{
    public int MaxItems { get; init; }
}