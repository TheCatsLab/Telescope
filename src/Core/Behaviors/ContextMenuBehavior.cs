using System.Windows;
using System.Windows.Controls;

namespace Cats.Telescope.VsExtension.Core.Behaviors;

/// <summary>
/// Attached behavior for context menus declared in a <c>Style</c> setter.
/// </summary>
public static class ContextMenuBehavior
{
    #region ClearPlacementTargetOnClose (Attached DependencyProperty)

    public static readonly DependencyProperty ClearPlacementTargetOnCloseProperty =
        DependencyProperty.RegisterAttached("ClearPlacementTargetOnClose", typeof(bool), typeof(ContextMenuBehavior),
            new PropertyMetadata(false, OnClearPlacementTargetOnCloseChanged));

    public static bool GetClearPlacementTargetOnClose(DependencyObject obj)
    {
        return (bool)obj.GetValue(ClearPlacementTargetOnCloseProperty);
    }

    public static void SetClearPlacementTargetOnClose(DependencyObject obj, bool value)
    {
        obj.SetValue(ClearPlacementTargetOnCloseProperty, value);
    }

    private static void OnClearPlacementTargetOnCloseChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ContextMenu menu)
            return;

        // the callback can run more than once for the same instance
        menu.Closed -= Menu_Closed;

        if (e.NewValue is true)
            menu.Closed += Menu_Closed;
    }

    private static void Menu_Closed(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu)
            return;

        // A ContextMenu declared in a Style setter is a single instance shared by every
        // item the style applies to. WPF stores the item it was opened for in
        // PlacementTarget and never clears it, so the shared menu rooted the last opened
        // item (and its whole subtree) for the lifetime of the tool window.
        menu.PlacementTarget = null;
        menu.DataContext = null;
    }

    #endregion
}
