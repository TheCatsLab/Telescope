using Cats.Telescope.VsExtension.Core.Enums;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Cats.Telescope.VsExtension.Core.Converters;

internal class NodeTypeToSvgImageConverter : IValueConverter
{
    private const string SubscriptionIconUri = "pack://application:,,,/Cats.Telescope.VsExtension;component/Resources/User-Subscriptions.svg";
    private const string ResourceGroupIconUri = "pack://application:,,,/Cats.Telescope.VsExtension;component/Resources/Resource-Groups.svg";
    private const string LogicAppIconUri = "pack://application:,,,/Cats.Telescope.VsExtension;component/Resources/Logic-Apps.svg";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        ResourceNodeType type = (ResourceNodeType)value;

        return type switch
        {
            ResourceNodeType.Subscription => SubscriptionIconUri,
            ResourceNodeType.ResourceGroup => ResourceGroupIconUri,
            ResourceNodeType.LogicApp => LogicAppIconUri,
            _ => throw new NotImplementedException()
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
