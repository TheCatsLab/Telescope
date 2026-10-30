using Cats.Telescope.VsExtension.Core.Enums;
using Cats.Telescope.VsExtension.Core.Extensions;
using System.Collections.Generic;
using System.Text.RegularExpressions;

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

        RegexOptions regexOptions = RegexOptions.None;

        if (filter.StringComparison.IsIgnoreCaseComparison())
            regexOptions ^= RegexOptions.IgnoreCase;

        string pattern = filter.EffectiveSearchText;

        // filter by name
        if (filter.FilterByOptions.HasFlag(FilterBy.ResourceName))
        {
            int findings = Regex.Matches(id, pattern, regexOptions).Count;

            if (findings > 0)
                result.Matches.Add(new Match(FilterBy.ResourceName, findings));
        }

        // filter by data
        if (filter.FilterByOptions.HasFlag(FilterBy.ResourceData))
        {
            int findings = Regex.Matches(data ?? string.Empty, pattern, regexOptions).Count;

            if (findings > 0)
                result.Matches.Add(new Match(FilterBy.ResourceData, findings));
        }

        // filter by tag keys
        if (filter.FilterByOptions.HasFlag(FilterBy.ResourceTagKeys) && tags?.Keys != null)
        {
            // the separator prevents a match spanning over two adjacent keys
            int findings = Regex.Matches(string.Join("\n", tags.Keys), pattern, regexOptions).Count;

            if (findings > 0)
                result.Matches.Add(new Match(FilterBy.ResourceTagKeys, findings));
        }

        // filter by tag values
        if (filter.FilterByOptions.HasFlag(FilterBy.ResourceTagValues) && tags?.Values != null)
        {
            int findings = Regex.Matches(string.Join("\n", tags.Values), pattern, regexOptions).Count;

            if (findings > 0)
                result.Matches.Add(new Match(FilterBy.ResourceTagValues, findings));
        }

        // filter by environment variable keys
        if (filter.FilterByOptions.HasFlag(FilterBy.ResourceEnvironmentVariableKeys) && environmentVariables?.Keys != null)
        {
            int findings = Regex.Matches(string.Join("\n", environmentVariables.Keys), pattern, regexOptions).Count;

            if (findings > 0)
                result.Matches.Add(new Match(FilterBy.ResourceEnvironmentVariableKeys, findings));
        }

        // filter by environment variable values
        if (filter.FilterByOptions.HasFlag(FilterBy.ResourceEnvironmentVariableValues) && environmentVariables?.Values != null)
        {
            int findings = Regex.Matches(string.Join("\n", environmentVariables.Values), pattern, regexOptions).Count;

            if (findings > 0)
                result.Matches.Add(new Match(FilterBy.ResourceEnvironmentVariableValues, findings));
        }

        return result;
    }
}
