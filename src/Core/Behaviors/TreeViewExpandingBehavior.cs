using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Cats.Telescope.VsExtension.Core.Behaviors;

public static class Behaviours
{
    #region ExpandingBehaviour (Attached DependencyProperty)

    public static readonly DependencyProperty ExpandingBehaviourProperty =
        DependencyProperty.RegisterAttached("ExpandingBehaviour", typeof(ICommand), typeof(Behaviours),
            new PropertyMetadata(OnExpandingBehaviourChanged));

    // holds the Expanded handler installed for the current command so it can be removed
    // when the property value changes - virtualized TreeViewItem containers are recycled,
    // so this callback fires again and again for the same container instance
    private static readonly DependencyProperty ExpandingHandlerProperty =
        DependencyProperty.RegisterAttached("ExpandingHandler", typeof(RoutedEventHandler), typeof(Behaviours),
            new PropertyMetadata(null));

    public static void SetExpandingBehaviour(DependencyObject o, ICommand value)
    {
        o.SetValue(ExpandingBehaviourProperty, value);
    }

    public static ICommand GetExpandingBehaviour(DependencyObject o)
    {
        return (ICommand)o.GetValue(ExpandingBehaviourProperty);
    }

    private static void OnExpandingBehaviourChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TreeViewItem tvi)
        {
            // remove the handler installed for the previous command, if any
            if (tvi.GetValue(ExpandingHandlerProperty) is RoutedEventHandler staleHandler)
                tvi.Expanded -= staleHandler;

            if (e.NewValue is ICommand ic)
            {
                RoutedEventHandler handler = (s, a) =>
                {
                    if (ic.CanExecute(a))
                    {
                        ic.Execute(a);

                    }
                    a.Handled = true;
                };

                tvi.Expanded += handler;
                tvi.SetValue(ExpandingHandlerProperty, handler);
            }
            else
            {
                tvi.SetValue(ExpandingHandlerProperty, null);
            }
        }
    }

    #endregion
}
