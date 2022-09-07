using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Cats.Telescope.VsExtension.Core.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isReverse = (parameter as string)?.ToUpper() == "REVERSE";
        return (bool)value ^ isReverse ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
