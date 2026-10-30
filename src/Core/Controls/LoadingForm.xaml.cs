using System.Windows;
using System.Windows.Controls;

namespace Cats.Telescope.VsExtension.Core.Controls;

/// <summary>
/// A themed loading form with a spinner and a text to be displayed while an operation is in progress
/// </summary>
public partial class LoadingForm : UserControl
{
    #region Text

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        "Text", typeof(string), typeof(LoadingForm), new PropertyMetadata("Loading..."));

    /// <summary>
    /// Text displayed above the spinner
    /// </summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    #endregion

    public LoadingForm()
    {
        InitializeComponent();
    }
}
