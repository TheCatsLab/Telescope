using Cats.Telescope.VsExtension.Core.Enums;

namespace Cats.Telescope.VsExtension.Core.Models;

/// <summary>
/// Presents thethe resource nodes tree filtering options
/// </summary>
internal class NodeFilter
{
    /// <summary>
    /// Text to filter the nodes by
    /// </summary>
    public string SearchText { get; set; }

    /// <summary>
    /// Indicates the node props to consider for filtering
    /// </summary>
    public FilterBy FilterByOptions { get; set; }

    public bool IsCaseSensitive { get; set; }
}
