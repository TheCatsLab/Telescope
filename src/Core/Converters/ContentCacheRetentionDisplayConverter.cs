using Cats.Telescope.VsExtension.Core.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace Cats.Telescope.VsExtension.Core.Converters;

/// <summary>
/// Converts a <see cref="ContentCacheRetention"/> value to its display label;
/// derives from <see cref="MarkupExtension"/> so every XAML usage gets its own instance,
/// following the convention of the other converters of the project
/// </summary>
internal class ContentCacheRetentionDisplayConverter : MarkupExtension, IValueConverter
{
    private static readonly IReadOnlyDictionary<ContentCacheRetention, string> Labels = new Dictionary<ContentCacheRetention, string>
    {
        [ContentCacheRetention.DoNotStore] = "Do not store",
        [ContentCacheRetention.OneHour] = "1 hour",
        [ContentCacheRetention.ThreeHours] = "3 hours",
        [ContentCacheRetention.SixHours] = "6 hours",
        [ContentCacheRetention.TwelveHours] = "12 hours",
        [ContentCacheRetention.TwentyFourHours] = "24 hours"
    };

    public override object ProvideValue(IServiceProvider serviceProvider) => this;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is ContentCacheRetention retention && Labels.TryGetValue(retention, out string label)
            ? label
            : string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // the combo box binds SelectedItem, so only the forward conversion is used
        throw new NotSupportedException();
    }
}
