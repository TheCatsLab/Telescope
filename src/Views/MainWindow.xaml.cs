using Cats.Telescope.VsExtension.ViewModels;
using Microsoft.Xaml.Behaviors;
using System;
using System.Threading.Tasks;
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
    public MainWindowControl(MainWindow toolWindowPane)
    {
        MainWindowViewModel viewModel = new()
        {
            ToolWindowPane = toolWindowPane
        };

        DataContext = viewModel;

        // required to load Microsoft.Xaml.Behaviors for usage in xaml

#pragma warning disable  CS0168 // the unused variables below are required 
        Behavior b;

        // required to load Community.VisualStudio.Toolkit for usage in xaml
        Community.VisualStudio.Toolkit.Windows a;

        // required to load SharpVectors.Converters.SvgViewbox for usage in xaml
        SharpVectors.Converters.SvgViewbox c;
#pragma warning restore CS0168

        this.InitializeComponent();

        viewModel.CopiedToClipboard += ViewModel_CopiedToClipboard;
        Loaded += MainWindowControl_Loaded;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "Async event handler")]
    private async void ViewModel_CopiedToClipboard(object sender, string e)
    {
        ClipboardPopup.PlacementTarget = e switch
        {
            "name" => CopyNameToClipboardButton,
            "data" => CopyDataToClipboardButton,
            "tree" => CopyNodesToClipboardButton,
            _ => throw new NotImplementedException()
        };
        ClipboardPopup.IsOpen = true;

        await Task.Delay(2000);

        ClipboardPopup.IsOpen = false;
    }

    private MainWindowViewModel ViewModel => DataContext as MainWindowViewModel;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "Async event handler")]
    private async void MainWindowControl_Loaded(object sender, RoutedEventArgs e)
    {
        if(ViewModel != null)
        {
            await ViewModel.OnLoadedAsync(null);
        }
    }
}