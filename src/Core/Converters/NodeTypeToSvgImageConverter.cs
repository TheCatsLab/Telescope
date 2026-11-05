using Cats.Telescope.VsExtension.Core.Enums;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows.Data;
using System.Windows.Media;

namespace Cats.Telescope.VsExtension.Core.Converters;

internal class NodeTypeToSvgImageConverter : IValueConverter
{
    // each icon is parsed from its SVG file once and shared by every tree container -
    // the SvgViewbox alternative re-read and re-parsed the file (DOM and the embedded CSS)
    // on every container realization, showing up as SharpVectors allocation churn
    private static readonly Dictionary<ResourceNodeType, ImageSource> IconCache = new();

    private static readonly object IconCacheSync = new();

    private static string SubscriptionIconUri = GetImageFullPath("User-Subscriptions.svg");
    private static string ResourceGroupIconUri = GetImageFullPath("Resource-Groups.svg");
    private static string LogicAppIconUri = GetImageFullPath("Logic-Apps.svg");
    private static string FunctionAppIconUri = GetImageFullPath("Function-Apps.svg");
    private static string WebServiceAppIconUri = GetImageFullPath("App-Services.svg");

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        ResourceNodeType type = (ResourceNodeType)value;

        lock (IconCacheSync)
        {
            if (IconCache.TryGetValue(type, out ImageSource cached))
                return cached;
        }

        string iconPath = type switch
        {
            ResourceNodeType.Subscription => SubscriptionIconUri,
            ResourceNodeType.ResourceGroup => ResourceGroupIconUri,
            ResourceNodeType.LogicApp => LogicAppIconUri,
            ResourceNodeType.Function => FunctionAppIconUri,
            ResourceNodeType.WebService => WebServiceAppIconUri,
            ResourceNodeType.DeploymentSlot => WebServiceAppIconUri,
            _ => null
        };

        if (iconPath is null)
            return null;

        ImageSource icon = LoadIcon(iconPath);

        if (icon != null)
        {
            lock (IconCacheSync)
            {
                IconCache[type] = icon;
            }
        }

        return icon;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Parses an SVG file into a frozen, shareable <see cref="ImageSource"/>
    /// </summary>
    private static ImageSource LoadIcon(string path)
    {
        try
        {
            DrawingGroup drawing = new FileSvgReader(new WpfDrawingSettings(), false).Read(path);

            if (drawing is null)
                return null;

            DrawingImage image = new DrawingImage(drawing);
            image.Freeze();

            return image;
        }
        catch
        {
            // a missing or broken icon must not break the tree; the node is left without one
            return null;
        }
    }

    private static string GetImageFullPath(string filename)
    {
        // the directory of the assembly - the resources folder sits next to the dll;
        // combining through the assembly file name itself produced a path that does
        // not exist(FileSvgReader throws on it, the old SvgViewbox silently skipped it)
        return Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "resources",
                filename);
    }
}
