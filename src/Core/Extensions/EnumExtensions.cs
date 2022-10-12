using System;

namespace Cats.Telescope.VsExtension.Core.Extensions;

internal static class EnumExtensions
{
    /// <summary>
    /// Returns true if the <paramref name="stringComparison"/> indicates about ingoring case while comparing strings
    /// </summary>
    /// <param name="stringComparison"></param>
    /// <returns></returns>
    public static bool IsIgnoreCaseComparison(this StringComparison stringComparison)
    {
        return stringComparison == StringComparison.OrdinalIgnoreCase
            || stringComparison == StringComparison.InvariantCultureIgnoreCase
            || stringComparison == StringComparison.CurrentCultureIgnoreCase;
    }
}
