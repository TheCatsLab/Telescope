using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Cats.Telescope.VsExtension.Core.Converters;
internal class PathDataToGeometryConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string pathData || pathData is null)
            return null;

        return Geometry.Parse(pathData);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

