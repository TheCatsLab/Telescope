using Cats.Telescope.VsExtension.Core.Enums;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Cats.Telescope.VsExtension.Core.Converters;

internal class FilterOptionFlagConverter : IValueConverter
{
    private FilterBy target;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        FilterBy mask = (FilterBy)parameter;
        this.target = (FilterBy)value;

        return ((mask & this.target) != 0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        this.target ^= (FilterBy)parameter;
        return this.target;
    }
}
