using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Diffy.App.Caching;

public interface ILRUCache<TKey, TValue>
{
    TValue Get(TKey key);
    void Set(TKey key, TValue value, long weight);
    void Remove(TKey key);
    void RemoveAll();
    long CurrentSize { get; }
    long MaxSize { get; }
    int Count { get; }
}

public class LruCache<TKey, TValue> : ILRUCache<TKey, TValue>
    where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, LRUCacheNode> _nodes = new();
    private readonly LinkedList<TKey> _order = new();
    private readonly long _maxSize;
    private long _currentSize;
    private readonly object _lock = new();

    public long MaxSize => _maxSize;
    public long CurrentSize => _currentSize;
    public int Count => _nodes.Count;

    public LruCache(long maxSize)
    {
        _maxSize = maxSize;
    }

    public TValue Get(TKey key)
    {
        if (!_nodes.TryGetValue(key, out var node))
            return default!;

        lock (_lock)
        {
            if (node.Value == null)
            {
                _nodes.TryRemove(key, out _);
                _order.Remove(key);
                _currentSize -= node.Weight;
                return default!;
            }

            MoveToFront(key);
            return node.Value!;
        }
    }

    public void Set(TKey key, TValue value, long weight)
    {
        if (weight <= 0)
            throw new ArgumentException("Weight must be positive", nameof(weight));

        lock (_lock)
        {
            if (_nodes.TryGetValue(key, out var existingNode))
            {
                existingNode.Value = value;
                existingNode.Weight = weight;
                MoveToFront(key);
                return;
            }

            EnsureCapacity(weight);

            var newNode = new LRUCacheNode
            {
                Key = key,
                Value = value,
                Weight = weight
            };

            _nodes[key] = newNode;
            _order.AddFirst(newNode.Key);
            _currentSize += weight;
        }
    }

    public void Remove(TKey key)
    {
        lock (_lock)
        {
            if (_nodes.TryRemove(key, out var node))
            {
                _order.Remove(key);
                _currentSize -= node.Weight;
            }
        }
    }

    public void RemoveAll()
    {
        lock (_lock)
        {
            _nodes.Clear();
            _order.Clear();
            _currentSize = 0;
        }
    }

    private void MoveToFront(TKey key)
    {
        if (!_order.Remove(key))
            return;

        _order.AddFirst(key);
    }

    private void EnsureCapacity(long requiredWeight)
    {
        while (_currentSize + requiredWeight > _maxSize && _order.Count > 0)
        {
            var lastNode = _order.Last;
            if (lastNode == null)
            {
                _order.RemoveLast();
                continue;
            }

            var lruKey = lastNode.Value;
            if (_nodes.TryRemove(lruKey, out var node))
            {
                _order.Remove(lruKey);
                _currentSize -= node.Weight;
            }
            else
            {
                _order.RemoveLast();
            }
        }
    }

    private class LRUCacheNode
    {
        public TKey Key { get; set; } = default!;
        public TValue? Value { get; set; }
        public long Weight { get; set; }
    }
}

public class StringLRUCache : ILRUCache<string, string>
{
    private readonly LruCache<string, string> _innerCache;
    private readonly Func<string, long> _weightCalculator;
    private long _totalGets;
    private long _totalHits;

    public string Get(string key)
    {
        _totalGets++;
        var value = _innerCache.Get(key);
        if (value != null)
        {
            _totalHits++;
        }
        return value ?? string.Empty;
    }

    public void Set(string key, string value, long weight)
    {
        if (weight <= 0)
            throw new ArgumentException("Weight must be positive", nameof(weight));

        _innerCache.Set(key, value, weight);
    }

    public void Remove(string key)
    {
        _innerCache.Remove(key);
    }

    public void RemoveAll()
    {
        _innerCache.RemoveAll();
    }

    public long CurrentSize => _innerCache.CurrentSize;
    public long MaxSize => _innerCache.MaxSize;
    public int Count => _innerCache.Count;
    public double HitRate => _totalGets == 0 ? 0 : (double)_totalHits / _totalGets;

    public StringLRUCache(long maxSize, Func<string, long>? weightCalculator = null)
    {
        _weightCalculator = weightCalculator ?? DefaultWeightCalculator;
        _innerCache = new LruCache<string, string>(maxSize);
    }

    private static long DefaultWeightCalculator(string key)
    {
        var length = key.Length * 2 + Math.Abs(key.GetHashCode());
        return length;
    }

    public static long CalculateContentWeight(string content)
    {
        if (string.IsNullOrEmpty(content))
            return 0;

        var lines = content.Split('\n');
        var totalWeight = lines.Length * 2 + content.Length;

        for (int i = 0; i < lines.Length; i++)
        {
            totalWeight += lines[i].Length * 2 + i * 3;
        }

        return totalWeight;
    }
}
