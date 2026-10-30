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
    /// Indicates if the search text is a regular expression;
    /// by default it is treated as literal text
    /// </summary>
    public bool IsRegex { get; set; }

    /// <summary>
    /// Returns the search text as a regex-safe pattern:
    /// a literal search escapes the text as is,
    /// an explicit regex is used as is when valid or escaped when broken
    /// </summary>
    public string EffectiveSearchText
    {
        get
        {
            if (_effectiveSearchText is null)
            {
                if (string.IsNullOrEmpty(SearchText))
                    return SearchText;

                if (!IsRegex)
                {
                    // the literal search - no metacharacter is interpreted
                    _effectiveSearchText = Regex.Escape(SearchText);
                }
                else
                {
                    try
                    {
                        // just validate the text is a correct regex pattern - nothing is matched here
                        Regex.Match(string.Empty, SearchText);

                        _effectiveSearchText = SearchText;
                    }
                    catch (ArgumentException)
                    {
                        // the pattern is broken, so search for it as a literal
                        _effectiveSearchText = Regex.Escape(SearchText);
                    }
                }
            }

            return _effectiveSearchText;
        }
    }
}
