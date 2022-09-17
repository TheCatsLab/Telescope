using System;

namespace Cats.Telescope.VsExtension.Core.Extensions;

public static class StringExtensions
{
    /// <summary>
    /// Returns <see cref="true"/> if <paramref name="source"/> string contains <paramref name="toCheck"/> value inside
    /// </summary>
    /// <param name="source">source string</param>
    /// <param name="toCheck">string to find</param>
    /// <param name="comp">string comparer</param>
    /// <returns></returns>
    public static bool Contains(this string source, string toCheck, StringComparison comp)
    {
        return source?.IndexOf(toCheck, comp) >= 0;
    }
}
