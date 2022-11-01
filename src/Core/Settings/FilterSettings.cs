using Cats.Telescope.VsExtension.Core.Enums;
using System;

namespace Cats.Telescope.VsExtension.Core.Settings;

sealed class ActiveFilterOptions
{
    public FilterBy FilterByOptions { get; set; }

    public bool IsCaseSensitive { get; set; }
}

/// <summary>
/// Contains definitions of the currently specified filter options
/// </summary>
sealed class FilterSettings : UISetting<ActiveFilterOptions>
{
    public FilterSettings()
    {

    }

    public FilterSettings(ActiveFilterOptions contentGridWidth)
    {
        OriginalValue = contentGridWidth;
    }
}
