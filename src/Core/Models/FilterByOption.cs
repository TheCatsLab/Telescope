using Cats.Telescope.VsExtension.Core.Enums;

namespace Cats.Telescope.VsExtension.Core.Models;

internal class FilterByOption
{
    public FilterByOption()
    {

    }

    public FilterByOption(string title, FilterBy value)
    {
        Title = title;
        Value = value;
    }

    public string Title { get; set; }

    public FilterBy Value { get; set; }

}
