using System;
using System.Globalization;
using System.Windows.Data;

namespace Cats.Telescope.VsExtension.Core.Converters;

internal class BoolReverseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool originalValue = (bool)value;
        return !originalValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
