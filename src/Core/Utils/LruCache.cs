using System;
using System.Collections.Generic;

namespace Cats.Telescope.VsExtension.Core.Utils;

/// <summary>
/// A thread-safe least-recently-used cache; hits move the entry to the front,
/// inserts beyond the capacity evict the least recently used entry
/// </summary>
internal sealed class LruCache<TKey, TValue> where TKey : class
{
    private readonly Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> _entries = new();
    private readonly LinkedList<KeyValuePair<TKey, TValue>> _order = new();
    private readonly object _sync = new();
    private readonly int _capacity;

    public LruCache(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        _capacity = capacity;
    }

    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>
    /// Returns the cached value and moves the entry to the most recently used position
    /// </summary>
    public bool TryGet(TKey key, out TValue value)
    {
        lock (_sync)
        {
            if (_entries.TryGetValue(key, out LinkedListNode<KeyValuePair<TKey, TValue>> node))
            {
                _order.Remove(node);
                _order.AddFirst(node);

                value = node.Value.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Adds or updates the entry as the most recently used one, evicting the
    /// least recently used entry when the capacity is exceeded
    /// </summary>
    public void Set(TKey key, TValue value)
    {
        lock (_sync)
        {
            if (_entries.TryGetValue(key, out LinkedListNode<KeyValuePair<TKey, TValue>> node))
            {
                _order.Remove(node);
                _order.AddFirst(node);
                node.Value = new KeyValuePair<TKey, TValue>(key, value);
                return;
            }

            if (_entries.Count >= _capacity)
            {
                LinkedListNode<KeyValuePair<TKey, TValue>> oldest = _order.Last;

                if (oldest != null)
                {
                    _order.RemoveLast();
                    _entries.Remove(oldest.Value.Key);
                }
            }

            LinkedListNode<KeyValuePair<TKey, TValue>> fresh = new(new KeyValuePair<TKey, TValue>(key, value));
            _order.AddFirst(fresh);
            _entries[key] = fresh;
        }
    }

    /// <summary>
    /// Drops every cached entry
    /// </summary>
    public void Clear()
    {
        lock (_sync)
        {
            _entries.Clear();
            _order.Clear();
        }
    }
}
