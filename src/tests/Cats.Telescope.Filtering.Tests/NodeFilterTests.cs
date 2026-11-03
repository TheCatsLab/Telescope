using Cats.Telescope.VsExtension.Core.Models;
using System.Text.RegularExpressions;
using Xunit;

namespace Cats.Telescope.Filtering.Tests;

public class NodeFilterTests
{
    [Theory]
    [InlineData("invoice")]      // plain text
    [InlineData(@"\d{3}")]       // valid regex
    [InlineData("(a|b)+")]       // valid alternation
    [InlineData("[a-z]+")]       // valid character class
    public void EffectiveSearchText_RegexMode_ValidPattern_ReturnedAsIs(string searchText)
    {
        NodeFilter filter = new() { SearchText = searchText, IsRegex = true };

        Assert.Equal(searchText, filter.EffectiveSearchText);
    }

    [Theory]
    [InlineData("invoice", "invoice")]
    [InlineData(@"\d{3}", @"\\d\{3}")]
    [InlineData("(a|b)+", @"\(a\|b\)\+")]
    public void EffectiveSearchText_DefaultMode_IsEscapedToLiteral(string searchText, string expected)
    {
        NodeFilter filter = new() { SearchText = searchText };

        Assert.Equal(expected, filter.EffectiveSearchText);
    }

    [Theory]
    [InlineData("(")]        // unbalanced group
    [InlineData("*")]        // quantifier without a preceding token
    [InlineData("a{2,1}")]   // invalid quantifier range
    public void EffectiveSearchText_RegexMode_InvalidPattern_EscapedToLiteral(string searchText)
    {
        NodeFilter filter = new() { SearchText = searchText };

        Assert.Equal(Regex.Escape(searchText), filter.EffectiveSearchText);
    }

    [Fact]
    public void EffectiveSearchText_EscapedLiteral_FindsPlainText()
    {
        NodeFilter filter = new() { SearchText = "(", IsRegex = true };

        // the escaped pattern must behave as a literal search
        Assert.Single(Regex.Matches("a ( b", filter.EffectiveSearchText));
    }

    [Fact]
    public void EffectiveSearchText_NullOrEmpty_ReturnedAsIs()
    {
        NodeFilter nullFilter = new() { SearchText = null };
        NodeFilter emptyFilter = new() { SearchText = string.Empty };

        Assert.Null(nullFilter.EffectiveSearchText);
        Assert.Equal(string.Empty, emptyFilter.EffectiveSearchText);
    }

    [Fact]
    public void EffectiveSearchText_IsCached()
    {
        NodeFilter filter = new() { SearchText = "(", IsRegex = true };

        string first = filter.EffectiveSearchText;
        filter.SearchText = "changed";

        // the value is computed once per filter instance
        Assert.Same(first, filter.EffectiveSearchText);
    }
}
