using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Cats.Telescope.VsExtension.Core.Converters;

internal class EnumToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null || !(value is Enum))
            return Visibility.Collapsed;

        var currentState = value.ToString();
        var stateString = parameter.ToString();
        var found = false;

        foreach (var state in currentState.Split(','))
        {
            found = (stateString == state.Trim());

            if (found)
                break;
        }

        return found;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
