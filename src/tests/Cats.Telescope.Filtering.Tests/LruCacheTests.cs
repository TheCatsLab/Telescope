using Cats.Telescope.VsExtension.Core.Utils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Cats.Telescope.Filtering.Tests;

public class LruCacheTests
{
    [Fact]
    public void Set_BeyondCapacity_EvictsLeastRecentlyUsed()
    {
        LruCache<string, int> cache = new(2);

        cache.Set("a", 1);
        cache.Set("b", 2);
        cache.Set("c", 3); // evicts "a"

        Assert.False(cache.TryGet("a", out int _));
        Assert.True(cache.TryGet("b", out int b));
        Assert.True(cache.TryGet("c", out int c));
        Assert.Equal(2, b);
        Assert.Equal(3, c);
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void TryGet_RefreshesRecency()
    {
        LruCache<string, int> cache = new(2);

        cache.Set("a", 1);
        cache.Set("b", 2);

        // touching "a" makes "b" the least recently used entry
        Assert.True(cache.TryGet("a", out int _));

        cache.Set("c", 3); // evicts "b", not "a"

        Assert.True(cache.TryGet("a", out int _));
        Assert.False(cache.TryGet("b", out int _));
        Assert.True(cache.TryGet("c", out int _));
    }

    [Fact]
    public void Set_ExistingKey_UpdatesValueWithoutGrowing()
    {
        LruCache<string, string> cache = new(2);

        cache.Set("a", "1");
        cache.Set("a", "2");

        Assert.True(cache.TryGet("a", out string value));
        Assert.Equal("2", value);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        LruCache<string, int> cache = new(4);

        cache.Set("a", 1);
        cache.Set("b", 2);
        cache.Clear();

        Assert.False(cache.TryGet("a", out int _));
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public async Task Concurrent_Access_StaysConsistent()
    {
        LruCache<string, int> cache = new(64);

        // hammering from parallel callers must not corrupt the internal structures
        await Task.Run(() =>
        {
            Parallel.For(0, 10_000, i =>
            {
                string key = $"key-{i % 128}";
                cache.Set(key, i);
                cache.TryGet(key, out int _);
            });
        });

        Assert.True(cache.Count <= 64);
    }
}
