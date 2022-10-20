using Cats.Telescope.VsExtension.Core;
using Cats.Telescope.VsExtension.Core.Settings;
using Cats.Telescope.VsExtension.Core.Utils;
using Cats.Telescope.VsExtension.ViewModels;
using Microsoft.Xaml.Behaviors;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Cats.Telescope.VsExtension.Views;

/// <summary>
/// Interaction logic for MainWindowControl.
/// </summary>
public partial class MainWindowControl : UserControl
{
    private readonly DispatcherTimer _resizeTimer = new() { Interval = new TimeSpan(0, 0, 0, 0, TelescopeConstants.Window.ResizeTimerInterval), IsEnabled = false };

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

        ContentGridSplitter.DragCompleted += ContentGridSplitter_DragCompleted;
        SizeChanged += MainWindowControl_SizeChanged;
        _resizeTimer.Tick += _resizeTimer_Tick;
    }

    private MainWindowViewModel ViewModel => DataContext as MainWindowViewModel;

    #region Event handlers

    private void MainWindowControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _resizeTimer.IsEnabled = true;
        _resizeTimer.Stop();
        _resizeTimer.Start();
    }

    void _resizeTimer_Tick(object sender, EventArgs e)
    {
        _resizeTimer.IsEnabled = false;

        try
        {
            WindowSizeSettings setting = new(new WindowSize { Height = this.ActualHeight, Width = this.ActualWidth });
            UserSettingsService.Instance.SetSetting(setting);
        }
        catch (Exception ex)
        {
            // todo: handle
            Debug.WriteLine(ex);
        }
    }

    private void ContentGridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        try
        {
            ContentGridWidthSetting setting = new(new ContentGridWidth { TreeColumnWidth = TreeGridColumn.Width.Value, ResourceDataColumnWidth = ResourceDataGridColumn.Width.Value });
            UserSettingsService.Instance.SetSetting(setting);
        }
        catch(Exception ex)
        {
            // todo: handle
            Debug.WriteLine(ex);
        }
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

        await Task.Delay(TelescopeConstants.Clipboard.PopupDefaultDisplayTime);

        ClipboardPopup.IsOpen = false;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "Async event handler")]
    private async void MainWindowControl_Loaded(object sender, RoutedEventArgs e)
    {
        await ApplyUISettingAsync();

        if (ViewModel != null)
        {
            await ViewModel.OnLoadedAsync(null);
        }        
    }

    #endregion

    #region Methods

    /// <summary>
    /// Applies all user settings to the window like the grid splitter position etc.
    /// </summary>
    private async Task ApplyUISettingAsync()
    {
        // set the grid splitter position
        ContentGridWidthSetting gridSettings = UserSettingsService.Instance.GetSetting<ContentGridWidthSetting>();
        if (gridSettings is not null)
        {
            TreeGridColumn.Width = new GridLength(gridSettings.OriginalValue.TreeColumnWidth);
            //ResourceDataGridColumn.Width = new GridLength(gridSettings.OriginalValue.ResourceDataColumnWidth);
        }

        // set the window size
        WindowSizeSettings windowSizeSettings = UserSettingsService.Instance.GetSetting<WindowSizeSettings>();
        if (windowSizeSettings is not null)
        {
            SizeChanged -= MainWindowControl_SizeChanged;

            await ViewModel.ToolWindowPane.SetThisWindowSizeAsync((int)windowSizeSettings.OriginalValue.Width, (int)windowSizeSettings.OriginalValue.Height);

            SizeChanged += MainWindowControl_SizeChanged;
        }
    }

    #endregion
}