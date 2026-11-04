using Cats.Telescope.VsExtension.Core.Enums;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RegexMatch = System.Text.RegularExpressions.Match;

namespace Cats.Telescope.VsExtension.Core.Models;

/// <summary>
/// Calculates how many times a <see cref="NodeFilter"/> matches a resource data snapshot.
/// Kept free of any UI or Visual Studio dependencies so the logic can be unit tested.
/// </summary>
internal static class NodeFilterMatcher
{
    /// <summary>
    /// Applies <paramref name="filter"/> to the resource data and calculates how many matches there are
    /// </summary>
    /// <param name="id">resource name</param>
    /// <param name="data">resource data(json) - can be null</param>
    /// <param name="tags">resource tags - can be null</param>
    /// <param name="environmentVariables">resource environment variables(app settings) - can be null</param>
    /// <param name="filter">node filter to apply</param>
    /// <returns></returns>
    public static NodeFilterResult MatchesCount(string id, string data, IReadOnlyDictionary<string, string> tags, IReadOnlyDictionary<string, string> environmentVariables, NodeFilter filter)
    {
        NodeFilterResult result = new();

        if (filter is null)
            return result;

        if (string.IsNullOrEmpty(filter.SearchText))
            return result;

        if (id is null)
            return result;

        // one matcher per filter instance: a pass over the tree reuses it for every node
        Regex matcher = filter.Matcher;

        if (matcher is null)
            return result;

        // filter by name
        if (filter.FilterByOptions.HasFlag(FilterBy.ResourceName))
            result.Add(FilterBy.ResourceName, CountMatches(matcher, id));

        // filter by data
        if (filter.FilterByOptions.HasFlag(FilterBy.ResourceData))
            result.Add(FilterBy.ResourceData, CountMatches(matcher, data ?? string.Empty));

        // filter by tag keys
        if (filter.FilterByOptions.HasFlag(FilterBy.ResourceTagKeys) && tags?.Keys != null)
        {
            // the separator prevents a match spanning over two adjacent keys
            result.Add(FilterBy.ResourceTagKeys, CountMatches(matcher, string.Join("\n", tags.Keys)));
        }

        // filter by tag values
        if (filter.FilterByOptions.HasFlag(FilterBy.ResourceTagValues) && tags?.Values != null)
            result.Add(FilterBy.ResourceTagValues, CountMatches(matcher, string.Join("\n", tags.Values)));

        // filter by environment variable keys
        if (filter.FilterByOptions.HasFlag(FilterBy.ResourceEnvironmentVariableKeys) && environmentVariables?.Keys != null)
            result.Add(FilterBy.ResourceEnvironmentVariableKeys, CountMatches(matcher, string.Join("\n", environmentVariables.Keys)));

        // filter by environment variable values
        if (filter.FilterByOptions.HasFlag(FilterBy.ResourceEnvironmentVariableValues) && environmentVariables?.Values != null)
            result.Add(FilterBy.ResourceEnvironmentVariableValues, CountMatches(matcher, string.Join("\n", environmentVariables.Values)));

        return result;
    }

    /// <summary>
    /// Counts the matches without materializing a <see cref="MatchCollection"/>
    /// </summary>
    private static int CountMatches(Regex regex, string input)
    {
        int count = 0;

        for (RegexMatch match = regex.Match(input); match.Success; match = match.NextMatch())
            count++;

        return count;
    }
}
