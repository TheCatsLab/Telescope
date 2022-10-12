using Cats.Telescope.VsExtension.Core.Enums;
using System.Collections.Generic;
using System.Linq;

namespace Cats.Telescope.VsExtension.Core.Models;

internal record struct NodeFilterResult
{
    public NodeFilterResult()
    {
        this.Matches = new();
    }

    public List<Match> Matches { get; }

    public bool Success => Matches?.Any(m => m.Count > 0) ?? false;
}

/// <summary>
/// Presents a match of a filter by <paramref name="By"/> <paramref name="Count"/> times
/// </summary>
/// <param name="By"></param>
/// <param name="Count"></param>
internal record struct Match(FilterBy By, int Count);

