using Cats.Telescope.VsExtension.Core.Enums;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace Cats.Telescope.VsExtension.Core.Converters;

/// <summary>
/// Converts a <see cref="FilterBy"/> flags value to/from a checkbox state.
/// Derives from <see cref="MarkupExtension"/> so every XAML usage gets its own instance -
/// the converter keeps the last converted value in a field, so a shared instance would mix up the bindings
/// </summary>
internal class FilterOptionFlagConverter : MarkupExtension, IValueConverter
{
    private FilterBy target;

    public override object ProvideValue(IServiceProvider serviceProvider) => this;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        FilterBy mask = ParseParameter(parameter);
        this.target = (FilterBy)value;

        return ((mask & this.target) != 0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        this.target ^= ParseParameter(parameter);
        return this.target;
    }

    /// <summary>
    /// Accepts a <see cref="FilterBy"/> value or its name(s), e.g. "ResourceTagKeys|ResourceTagValues" for grouped scopes
    /// </summary>
    private static FilterBy ParseParameter(object parameter)
    {
        if (parameter is FilterBy flags)
            return flags;

        return (FilterBy)Enum.Parse(typeof(FilterBy), parameter.ToString().Replace('|', ','), false);
    }
}
