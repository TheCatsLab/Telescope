using System.Windows;
using System.Windows.Controls;

namespace Cats.Telescope.VsExtension.Core.Controls;

internal class PathDataButton : Button
{
    static PathDataButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(PathDataButton), new FrameworkPropertyMetadata(typeof(PathDataButton)));
    }

    #region Path Data

    internal static readonly DependencyProperty PathDataProperty = DependencyProperty.Register(
        "PathData", typeof(string), typeof(PathDataButton), new PropertyMetadata(null, OnPathDataChanged));

    private static void OnPathDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
    }

    public string PathData
    {
        get => (string)GetValue(PathDataProperty);
        set => SetValue(PathDataProperty, value);
    }

    #endregion
}
