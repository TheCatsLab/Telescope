using Cats.Telescope.VsExtension.Core.Enums;

namespace Cats.Telescope.VsExtension.Core.Models;

internal class FilterTargetOption
{
    public FilterTargetOption()
    {

    }

    public FilterTargetOption(string title, FilterBy value)
    {
        Title = title;
        Value = value;
    }

    public string Title { get; set; }

    public FilterBy Value { get; set; }
}
