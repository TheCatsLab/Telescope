using Cats.Telescope.VsExtension.ViewModels;
using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;

namespace Cats.Telescope.VsExtension.Views;

/// <summary>
/// Interaction logic for MainWindowControl.
/// </summary>
public partial class MainWindowControl : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowControl"/> class.
    /// </summary>
    public MainWindowControl()
    {
        DataContext = new MainWindowViewModel();

        Behavior b;
        Community.VisualStudio.Toolkit.Windows a;

        this.InitializeComponent();

        Loaded += MainWindowControl_Loaded;
    }

    private MainWindowViewModel ViewModel => DataContext as MainWindowViewModel;

    private async void MainWindowControl_Loaded(object sender, RoutedEventArgs e)
    {
        if(ViewModel != null)
        {
            await ViewModel.OnLoadedAsync(null);
        }
    }
}