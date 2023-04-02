using Cats.Telescope.VsExtension.Core.Enums;
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows.Data;

namespace Cats.Telescope.VsExtension.Core.Converters;

internal class NodeTypeToSvgImageConverter : IValueConverter
{
    private static string SubscriptionIconUri = GetImageFullPath("User-Subscriptions.svg");
    private static string ResourceGroupIconUri = GetImageFullPath("Resource-Groups.svg");
    private static string LogicAppIconUri = GetImageFullPath("Logic-Apps.svg");
    private static string FunctionAppIconUri = GetImageFullPath("Function-Apps.svg");
    private static string WebServiceAppIconUri = GetImageFullPath("App-Services.svg");

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        ResourceNodeType type = (ResourceNodeType)value;

        return type switch
        {
            ResourceNodeType.Subscription => SubscriptionIconUri,
            ResourceNodeType.ResourceGroup => ResourceGroupIconUri,
            ResourceNodeType.LogicApp => LogicAppIconUri,
            ResourceNodeType.Function => FunctionAppIconUri,
            ResourceNodeType.WebService => WebServiceAppIconUri,
            _ => null
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static string GetImageFullPath(string filename)
    {
        return Path.Combine(
                //Get the location of your package dll
                Assembly.GetExecutingAssembly().Location,
                //reference your 'images' folder
                "/resources/",
                filename
             );
    }
}
