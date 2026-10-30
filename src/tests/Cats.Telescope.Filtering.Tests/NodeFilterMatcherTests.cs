using Cats.Telescope.VsExtension.Core.Enums;
using Cats.Telescope.VsExtension.Core.Models;
using System;
using System.Collections.Generic;
using Xunit;

namespace Cats.Telescope.Filtering.Tests;

public class NodeFilterMatcherTests
{
    private const string ResourceId = "invoice-processor-prod";

    private static NodeFilter MakeFilter(string searchText, FilterBy filterBy, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        => new() { SearchText = searchText, FilterByOptions = filterBy, StringComparison = comparison };

    #region Guards

    [Fact]
    public void MatchesCount_NullFilter_ReturnsEmptyResult()
    {
        NodeFilterResult result = NodeFilterMatcher.MatchesCount(ResourceId, "{}", null, null, null);

        Assert.False(result.Success);
    }

    [Fact]
    public void MatchesCount_EmptySearchText_ReturnsEmptyResult()
    {
        NodeFilterResult result = NodeFilterMatcher.MatchesCount(ResourceId, "{}", null, null, MakeFilter(string.Empty, FilterBy.ResourceName));

        Assert.False(result.Success);
    }

    [Fact]
    public void MatchesCount_NullId_ReturnsEmptyResult()
    {
        NodeFilterResult result = NodeFilterMatcher.MatchesCount(null, "{}", null, null, MakeFilter("invoice", FilterBy.ResourceName));

        Assert.False(result.Success);
    }

    [Fact]
    public void MatchesCount_AllNullSources_DoesNotThrow()
    {
        NodeFilter filter = MakeFilter("invoice", FilterBy.ResourceName | FilterBy.ResourceData | FilterBy.ResourceTagKeys | FilterBy.ResourceTagValues | FilterBy.ResourceEnvironmentVariableKeys | FilterBy.ResourceEnvironmentVariableValues);

        NodeFilterResult result = NodeFilterMatcher.MatchesCount(ResourceId, null, null, null, filter);

        // only the resource name has been searched - everything else is just skipped without exceptions
        Assert.True(result.Success);
        Assert.Single(result.Matches);
        Assert.Equal(FilterBy.ResourceName, result.Matches[0].By);
    }

    #endregion

    #region Invalid regex patterns are treated as literal text

    [Theory]
    [InlineData("(")]        // unbalanced group
    [InlineData("*")]        // quantifier without a preceding token
    [InlineData("[a-")]      // unterminated character class
    [InlineData("a{2,1}")]   // invalid quantifier range
    public void MatchesCount_InvalidRegexPattern_DoesNotThrow_AndMatchesAsLiteral(string searchText)
    {
        string data = $"prefix {searchText} suffix";
        NodeFilter filter = MakeFilter(searchText, FilterBy.ResourceData);

        NodeFilterResult result = NodeFilterMatcher.MatchesCount(ResourceId, data, null, null, filter);

        Assert.True(result.Success);
        Assert.Equal(1, result.Matches.Find(m => m.By == FilterBy.ResourceData).Count);
    }

    [Fact]
    public void MatchesCount_InvalidRegexPattern_DoesNotMatchWhenLiteralIsAbsent()
    {
        NodeFilter filter = MakeFilter("(", FilterBy.ResourceData | FilterBy.ResourceName);

        NodeFilterResult result = NodeFilterMatcher.MatchesCount(ResourceId, "no such symbol here", null, null, filter);

        Assert.False(result.Success);
    }

    #endregion

    #region Valid regex patterns keep working

    [Fact]
    public void MatchesCount_ValidRegexPattern_IsStillApplied()
    {
        NodeFilter filter = MakeFilter(@"\d{3}", FilterBy.ResourceData);

        NodeFilterResult result = NodeFilterMatcher.MatchesCount(ResourceId, "abc123def456", null, null, filter);

        Assert.True(result.Success);
        Assert.Equal(2, result.Matches.Find(m => m.By == FilterBy.ResourceData).Count);
    }

    [Fact]
    public void MatchesCount_AlternationRegex_IsStillApplied()
    {
        NodeFilter filter = MakeFilter("invoice|billing", FilterBy.ResourceName);

        NodeFilterResult result = NodeFilterMatcher.MatchesCount(ResourceId, null, null, null, filter);

        Assert.True(result.Success);
        Assert.Equal(1, result.Matches.Find(m => m.By == FilterBy.ResourceName).Count);
    }

    #endregion

    #region Null resource data does not break the filtering

    [Fact]
    public void MatchesCount_NullData_WithResourceDataFlag_DoesNotThrow()
    {
        NodeFilter filter = MakeFilter("anything", FilterBy.ResourceData);

        NodeFilterResult result = NodeFilterMatcher.MatchesCount(ResourceId, null, null, null, filter);

        Assert.False(result.Success);
    }

    #endregion

    #region No false positives across dictionary entry boundaries

    [Fact]
    public void MatchesCount_TagKeys_QuerySpanningTwoKeys_DoesNotMatch()
    {
        // "dev" + "prod" concatenated by mistake would contain "devprod"
        Dictionary<string, string> tags = new() { { "dev", "1" }, { "prod", "2" } };
        NodeFilter filter = MakeFilter("devprod", FilterBy.ResourceTagKeys);

        NodeFilterResult result = NodeFilterMatcher.MatchesCount(ResourceId, null, tags, null, filter);

        Assert.False(result.Success);
    }

    [Fact]
    public void MatchesCount_TagValues_QuerySpanningTwoValues_DoesNotMatch()
    {
        Dictionary<string, string> tags = new() { { "a", "dev" }, { "b", "prod" } };
        NodeFilter filter = MakeFilter("devprod", FilterBy.ResourceTagValues);

        NodeFilterResult result = NodeFilterMatcher.MatchesCount(ResourceId, null, tags, null, filter);

        Assert.False(result.Success);
    }

    [Fact]
    public void MatchesCount_EnvironmentVariableKeys_QuerySpanningTwoKeys_DoesNotMatch()
    {
        Dictionary<string, string> envVars = new() { { "env_dev", "1" }, { "env_prod", "2" } };
        NodeFilter filter = MakeFilter("env_devenv_prod", FilterBy.ResourceEnvironmentVariableKeys);

        NodeFilterResult result = NodeFilterMatcher.MatchesCount(ResourceId, null, null, envVars, filter);

        Assert.False(result.Success);
    }

    [Fact]
    public void MatchesCount_EnvironmentVariableValues_QuerySpanningTwoValues_DoesNotMatch()
    {
        Dictionary<string, string> envVars = new() { { "a", "AAA" }, { "b", "BBB" } };
        NodeFilter filter = MakeFilter("AAABBB", FilterBy.ResourceEnvironmentVariableValues);

        NodeFilterResult result = NodeFilterMatcher.MatchesCount(ResourceId, null, null, envVars, filter);

        Assert.False(result.Success);
    }

    [Fact]
    public void MatchesCount_EnvironmentVariableKeys_CommonPrefix_MatchesAllKeys()
    {
        Dictionary<string, string> envVars = new() { { "env_dev", "1" }, { "env_prod", "2" } };
        NodeFilter filter = MakeFilter("env_", FilterBy.ResourceEnvironmentVariableKeys);

        NodeFilterResult result = NodeFilterMatcher.MatchesCount(ResourceId, null, null, envVars, filter);

        Assert.True(result.Success);
        Assert.Equal(2, result.Matches.Find(m => m.By == FilterBy.ResourceEnvironmentVariableKeys).Count);
    }

    #endregion

    #region Case sensitivity

    [Fact]
    public void MatchesCount_IgnoreCase_Comparison_MatchesDifferentCase()
    {
        NodeFilter filter = MakeFilter("INVOICE", FilterBy.ResourceName, StringComparison.OrdinalIgnoreCase);

        NodeFilterResult result = NodeFilterMatcher.MatchesCount(ResourceId, null, null, null, filter);

        Assert.True(result.Success);
    }

    [Fact]
    public void MatchesCount_CaseSensitive_Comparison_DoesNotMatchDifferentCase()
    {
        NodeFilter filter = MakeFilter("INVOICE", FilterBy.ResourceName, StringComparison.Ordinal);

        NodeFilterResult result = NodeFilterMatcher.MatchesCount(ResourceId, null, null, null, filter);

        Assert.False(result.Success);
    }

    #endregion

    #region Filter flags independence

    [Fact]
    public void MatchesCount_OnlySelectedFlagsProduceMatches()
    {
        Dictionary<string, string> tags = new() { { "team", "cats" } };
        NodeFilter filter = MakeFilter("team", FilterBy.ResourceTagKeys);

        // the data contains the text as well, but the flag is not set
        NodeFilterResult result = NodeFilterMatcher.MatchesCount(ResourceId, "team is mentioned here", tags, null, filter);

        Assert.True(result.Success);
        Assert.Single(result.Matches);
        Assert.Equal(FilterBy.ResourceTagKeys, result.Matches[0].By);
    }

    #endregion
}
