using Cats.Telescope.VsExtension.Core.Enums;
using System;
using System.Text.RegularExpressions;

namespace Cats.Telescope.VsExtension.Core.Models;

/// <summary>
/// Presents the resource nodes tree filtering options
/// </summary>
internal class NodeFilter
{
    private string _effectiveSearchText;

    /// <summary>
    /// Text to filter the nodes by
    /// </summary>
    public string SearchText { get; set; }

    /// <summary>
    /// Indicates the node props to consider for filtering
    /// </summary>
    public FilterBy FilterByOptions { get; set; }

    public StringComparison StringComparison { get; set; }

    /// <summary>
    /// Returns the search text as a regex-safe pattern:
    /// a valid regex is used as is, an invalid one is escaped to be treated as literal text
    /// </summary>
    public string EffectiveSearchText
    {
        get
        {
            if (_effectiveSearchText is null)
            {
                if (string.IsNullOrEmpty(SearchText))
                    return SearchText;

                try
                {
                    // just validate the text is a correct regex pattern - nothing is matched here
                    Regex.Match(string.Empty, SearchText);

                    _effectiveSearchText = SearchText;
                }
                catch (ArgumentException)
                {
                    // the text is not a valid pattern, so search for it as a literal
                    _effectiveSearchText = Regex.Escape(SearchText);
                }
            }

            return _effectiveSearchText;
        }
    }
}
