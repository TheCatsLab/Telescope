using Cats.Telescope.VsExtension.Core.Enums;
using System.Collections.Generic;

namespace Cats.Telescope.VsExtension.Core.Models;

internal record struct NodeFilterResult
{
    // A filter pass creates one result per tree node, while only a small fraction of
    // the nodes actually match. Sharing the empty list keeps non-matching results
    // allocation-free. Callers must treat it as read-only.
    private static readonly List<Match> EmptyMatches = new();

    private List<Match> _matches;

    /// <summary>
    /// Matches grouped by the filter source; never null
    /// </summary>
    public List<Match> Matches => _matches ?? EmptyMatches;

    /// <summary>
    /// Indicates if the filter found anything in this node
    /// </summary>
    public bool Success => _matches is { Count: > 0 };

    public int NameCount { get; private set; }

    public int DataCount { get; private set; }

    public int TagsCount { get; private set; }

    public int EnvironmentVariablesCount { get; private set; }

    /// <summary>
    /// Records a match; non-positive counts are ignored
    /// </summary>
    public void Add(FilterBy by, int count)
    {
        if (count <= 0)
            return;

        (_matches ??= new List<Match>()).Add(new Match(by, count));

        switch (by)
        {
            case FilterBy.ResourceName:
                NameCount += count;
                break;
            case FilterBy.ResourceData:
                DataCount += count;
                break;
            case FilterBy.ResourceTagKeys:
            case FilterBy.ResourceTagValues:
                TagsCount += count;
                break;
            case FilterBy.ResourceEnvironmentVariableKeys:
            case FilterBy.ResourceEnvironmentVariableValues:
                EnvironmentVariablesCount += count;
                break;
        }
    }
}

/// <summary>
/// Presents a match of a filter by <paramref name="By"/> <paramref name="Count"/> times
/// </summary>
/// <param name="By"></param>
/// <param name="Count"></param>
internal record struct Match(FilterBy By, int Count);
