using System;

namespace Cats.Telescope.VsExtension.Core.Controls;

internal class FilterOptions
{
    public string QueryText { get; set; }
    public StringComparison StringComparison { get; set; }

    /// <summary>
    /// Indicates if the query text should be treated as a regular expression;
    /// by default it is searched as literal text
    /// </summary>
    public bool UseRegex { get; set; }
}
