using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Cats.Telescope.VsExtension.Core.Utils;

/// <summary>
/// A thread-safe least-recently-used cache; hits move the entry to the front,
/// inserts beyond the capacity evict the least recently used entry; an optional
/// retention time makes entries expire after their creation, so the cached content
/// (often secrets) does not outlive the user-selected retention
/// </summary>
internal sealed class LruCache<TKey, TValue> where TKey : class
{
    private readonly Dictionary<TKey, LinkedListNode<CacheEntry>> _entries = new();
    private readonly LinkedList<CacheEntry> _order = new();
    private readonly object _sync = new();
    private readonly int _capacity;
    private readonly Func<long> _clock;

    private TimeSpan? _retention;

    public LruCache(int capacity, Func<long> clock = null)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        _capacity = capacity;

        // a monotonic timestamp in ticks; unlike Environment.TickCount it never wraps
        _clock = clock ?? (() => Stopwatch.GetTimestamp());
    }

    /// <summary>
    /// How long an entry stays valid since its creation, or null when entries never expire
    /// </summary>
    public TimeSpan? Retention
    {
        get
        {
            lock (_sync)
            {
                return _retention;
            }
        }
        set
        {
            lock (_sync)
            {
                _retention = value;
            }
        }
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
    /// Returns the cached value and moves the entry to the most recently used position;
    /// an expired entry is dropped right away and reported as missing
    /// </summary>
    public bool TryGet(TKey key, out TValue value)
    {
        lock (_sync)
        {
            if (_entries.TryGetValue(key, out LinkedListNode<CacheEntry> node))
            {
                long now = _clock();

                // the cached content is often secret - an expired entry must not linger
                // until some future access or eviction releases it
                if (IsExpired(node.Value, now))
                {
                    RemoveNode(node);

                    value = default;
                    return false;
                }

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
    /// Adds or updates the entry as the most recently used one, releasing the expired
    /// entries and evicting the least recently used one when the capacity is exceeded
    /// </summary>
    public void Set(TKey key, TValue value)
    {
        lock (_sync)
        {
            long now = _clock();

            if (_entries.TryGetValue(key, out LinkedListNode<CacheEntry> node))
            {
                _order.Remove(node);
                _order.AddFirst(node);
                node.Value = new CacheEntry(key, value, now);
                return;
            }

            PruneExpiredLocked(now);

            if (_entries.Count >= _capacity)
            {
                LinkedListNode<CacheEntry> oldest = _order.Last;

                if (oldest != null)
                {
                    _order.RemoveLast();
                    _entries.Remove(oldest.Value.Key);
                }
            }

            LinkedListNode<CacheEntry> fresh = new(new CacheEntry(key, value, now));
            _order.AddFirst(fresh);
            _entries[key] = fresh;
        }
    }

    /// <summary>
    /// Drops the entries that have outlived the retention; lets the owner apply a
    /// shortened retention immediately instead of waiting for the next access
    /// </summary>
    public void RemoveExpired()
    {
        lock (_sync)
        {
            PruneExpiredLocked(_clock());
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

    private bool IsExpired(CacheEntry entry, long now)
    {
        return _retention.HasValue && now - entry.CreatedTimestamp >= _retention.Value.Ticks;
    }

    /// <summary>
    /// The order list is recency-ordered, not age-ordered - a touched but old entry may
    /// sit anywhere in it, so the whole list has to be scanned
    /// </summary>
    private void PruneExpiredLocked(long now)
    {
        if (!_retention.HasValue)
            return;

        LinkedListNode<CacheEntry> node = _order.First;

        while (node != null)
        {
            LinkedListNode<CacheEntry> next = node.Next;

            if (IsExpired(node.Value, now))
                RemoveNode(node);

            node = next;
        }
    }

    private void RemoveNode(LinkedListNode<CacheEntry> node)
    {
        _order.Remove(node);
        _entries.Remove(node.Value.Key);
    }

    /// <summary>
    /// A single cache entry; the node value is replaced in place on update, so the
    /// struct must stay immutable to keep the timestamp from being lost
    /// </summary>
    private readonly struct CacheEntry
    {
        public CacheEntry(TKey key, TValue value, long createdTimestamp)
        {
            Key = key;
            Value = value;
            CreatedTimestamp = createdTimestamp;
        }

        public TKey Key { get; }

        public TValue Value { get; }

        public long CreatedTimestamp { get; }
    }
}
