using ICSharpCode.AvalonEdit.Document;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Cats.Telescope.VsExtension.Core.Converters
{
    internal class JsonToDocumentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if(!string.IsNullOrWhiteSpace(value as string))
                return new TextDocument(value as string);

            return new TextDocument();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
