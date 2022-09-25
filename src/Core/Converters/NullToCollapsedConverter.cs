using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Cats.Telescope.VsExtension.Core.Converters;

internal class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isReverse = (parameter as string)?.ToUpper() == "REVERSE";
        bool hasValue = false;

        if(value is string)
        {
            hasValue = !string.IsNullOrEmpty(value as string);
        }
        else
        {
            hasValue = value is not null;
        }

        return hasValue ^ isReverse ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
