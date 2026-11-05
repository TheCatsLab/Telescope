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

    [Fact]
    public void TryGet_EntryWithinRetention_IsServed()
    {
        long now = 0;
        LruCache<string, int> cache = new(4, () => now);
        cache.Retention = TimeSpan.FromMinutes(30);

        cache.Set("a", 1);

        now += TimeSpan.FromMinutes(29).Ticks;

        Assert.True(cache.TryGet("a", out int value));
        Assert.Equal(1, value);
    }

    [Fact]
    public void TryGet_ExpiredEntry_IsDroppedAndReportedAsMissing()
    {
        long now = 0;
        LruCache<string, int> cache = new(4, () => now);
        cache.Retention = TimeSpan.FromMinutes(30);

        cache.Set("a", 1);

        now += TimeSpan.FromMinutes(30).Ticks;

        Assert.False(cache.TryGet("a", out int _));
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void TryGet_DoesNotExtendRetention()
    {
        long now = 0;
        LruCache<string, int> cache = new(4, () => now);
        cache.Retention = TimeSpan.FromMinutes(30);

        cache.Set("a", 1);

        // a touch moves the entry to the front but must not restart its retention clock
        now += TimeSpan.FromMinutes(29).Ticks;
        Assert.True(cache.TryGet("a", out int _));

        now += TimeSpan.FromMinutes(1).Ticks;
        Assert.False(cache.TryGet("a", out int _));
    }

    [Fact]
    public void Set_ExpiredEntries_ArePrunedWithoutBeingTouched()
    {
        long now = 0;
        LruCache<string, int> cache = new(4, () => now);
        cache.Retention = TimeSpan.FromMinutes(30);

        cache.Set("a", 1);

        now += TimeSpan.FromMinutes(5).Ticks;
        cache.Set("b", 2);

        // at 31 minutes "a" is expired (31 min old) while "b" (26 min old) is not
        now += TimeSpan.FromMinutes(26).Ticks;
        cache.Set("c", 3); // the insert releases the expired "a" even though nobody read it

        Assert.Equal(2, cache.Count);
        Assert.False(cache.TryGet("a", out int _));
        Assert.True(cache.TryGet("b", out int _));
        Assert.True(cache.TryGet("c", out int _));
    }

    [Fact]
    public void Set_ExistingExpiredEntry_IsReplacedWithFresh()
    {
        long now = 0;
        LruCache<string, int> cache = new(4, () => now);
        cache.Retention = TimeSpan.FromMinutes(30);

        cache.Set("a", 1);

        now += TimeSpan.FromMinutes(31).Ticks;
        cache.Set("a", 2);

        now += TimeSpan.FromMinutes(1).Ticks;
        Assert.True(cache.TryGet("a", out int value));
        Assert.Equal(2, value);
    }

    [Fact]
    public void RemoveExpired_DropsOnlyExpiredEntries()
    {
        long now = 0;
        LruCache<string, int> cache = new(4, () => now);
        cache.Retention = TimeSpan.FromMinutes(30);

        cache.Set("a", 1);

        now += TimeSpan.FromMinutes(5).Ticks;
        cache.Set("b", 2);

        // at 31 minutes "a" is expired (31 min old) while "b" (26 min old) is not;
        // no Set happens past the expiry, so only RemoveExpired can drop "a"
        now += TimeSpan.FromMinutes(26).Ticks;
        cache.RemoveExpired();

        Assert.Equal(1, cache.Count);
        Assert.False(cache.TryGet("a", out int _));
        Assert.True(cache.TryGet("b", out int _));
    }

    [Fact]
    public void Retention_Null_NeverExpires()
    {
        long now = 0;
        LruCache<string, int> cache = new(4, () => now);

        cache.Set("a", 1);

        now += TimeSpan.FromHours(24).Ticks;

        Assert.True(cache.TryGet("a", out int value));
        Assert.Equal(1, value);
        Assert.Equal(1, cache.Count);
    }
}
