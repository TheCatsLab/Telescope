using Cats.Telescope.VsExtension.Core.Enums;
using Cats.Telescope.VsExtension.Core.Models;
using System;
using System.Collections.Generic;
using Xunit;

namespace Cats.Telescope.Filtering.Tests;

public class NodeFilterResultAllocationTests
{
    private const FilterBy AllSources =
        FilterBy.ResourceName |
        FilterBy.ResourceData |
        FilterBy.ResourceTagKeys |
        FilterBy.ResourceTagValues |
        FilterBy.ResourceEnvironmentVariableKeys |
        FilterBy.ResourceEnvironmentVariableValues;

    private static NodeFilter MakeFilter(string searchText, FilterBy filterBy)
        => new() { SearchText = searchText, FilterByOptions = filterBy, StringComparison = StringComparison.OrdinalIgnoreCase };

    [Fact]
    public void NonMatchingResults_ShareTheEmptyMatchesList()
    {
        NodeFilter filter = MakeFilter("zzz-no-such-symbol", AllSources);

        NodeFilterResult first = NodeFilterMatcher.MatchesCount("app-one", "{}", null, null, filter);
        NodeFilterResult second = NodeFilterMatcher.MatchesCount("app-two", "{}", null, null, filter);

        Assert.False(first.Success);
        Assert.False(second.Success);

        // a filter pass creates one result per node - the non-matching ones must not
        // allocate their own list
        Assert.Same(first.Matches, second.Matches);
        Assert.Empty(first.Matches);
    }

    [Fact]
    public void MatchesCount_ExposesPerSourceTotals()
    {
        NodeFilter filter = MakeFilter("team", AllSources);

        Dictionary<string, string> tags = new()
        {
            ["teamKey"] = "teamValue"
        };

        Dictionary<string, string> environmentVariables = new()
        {
            ["TEAM_ENV"] = "team-secret"
        };

        NodeFilterResult result = NodeFilterMatcher.MatchesCount("team-app", "team config", tags, environmentVariables, filter);

        Assert.True(result.Success);
        Assert.Equal(1, result.NameCount);
        Assert.Equal(1, result.DataCount);
        // both the key and the value contain "team"
        Assert.Equal(2, result.TagsCount);
        Assert.Equal(2, result.EnvironmentVariablesCount);
    }

    [Fact]
    public void MatchesCount_NonMatchingPass_DoesNotGrowWithNodeCount()
    {
        NodeFilter filter = MakeFilter("zzz-no-such-symbol", AllSources);

        Dictionary<string, string> tags = new() { ["owner"] = "team" };
        Dictionary<string, string> environmentVariables = new() { ["A"] = "b" };

        // warm up the regex and the JIT before measuring
        for (int i = 0; i < 1_000; i++)
            NodeFilterMatcher.MatchesCount("app-" + i, "{}", tags, environmentVariables, filter);

        long before = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < 10_000; i++)
            NodeFilterMatcher.MatchesCount("app-" + i, "{}", tags, environmentVariables, filter);

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Regression guard: before the change every result allocated a List<Match> and
        // every source allocated a MatchCollection. The remaining allocation is the
        // joined tag/environment strings and the regex Match objects.
        Assert.True(allocated < 8_000_000, $"allocated {allocated} bytes for 10000 nodes");
    }
}
