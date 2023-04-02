using Cats.Telescope.VsExtension.Core;
using Cats.Telescope.VsExtension.Core.Controls.AvalonEdit;
using Cats.Telescope.VsExtension.Core.Extensions;
using Cats.Telescope.VsExtension.Core.Settings;
using Cats.Telescope.VsExtension.Core.Utils;
using Cats.Telescope.VsExtension.ViewModels;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using Microsoft.Xaml.Behaviors;
using System;
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

        // required to load ICSharpCode.AvalonEdit.Document for usage in xaml
        TextDocument td;
#pragma warning restore CS0168


        this.InitializeComponent();

        ConfigurationTextEditor.TextArea.TextView.LineTransformers.Add(new ColorizeAvalonEdit());

        viewModel.CopiedToClipboard += ViewModel_CopiedToClipboard;
        Loaded += MainWindowControl_Loaded;

        ContentGridSplitter.DragCompleted += ContentGridSplitter_DragCompleted;
        SizeChanged += MainWindowControl_SizeChanged;
        _resizeTimer.Tick += ResizeTimer_Tick;

        viewModel.FilterSettingsChanged += ViewModel_FilterSettingsChanged;
    }

    private MainWindowViewModel ViewModel => DataContext as MainWindowViewModel;

    #region Event handlers

    private void MainWindowControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _resizeTimer.IsEnabled = true;
        _resizeTimer.Stop();
        _resizeTimer.Start();
    }

    private void ViewModel_FilterSettingsChanged(object sender, ActiveFilterOptions e)
    {
        try
        {
            FilterSettings filterSettings = new(e);
            UserSettingsService.Instance.SetSetting(filterSettings);
        }
        catch (Exception ex)
        {
            ex.LogAsync().Forget();
        }
    }

    void ResizeTimer_Tick(object sender, EventArgs e)
    {
        _resizeTimer.IsEnabled = false;

        try
        {
            WindowSizeSettings setting = new(new WindowSize { Height = this.ActualHeight, Width = this.ActualWidth });
            UserSettingsService.Instance.SetSetting(setting);
        }
        catch (Exception ex)
        {
            ex.LogAsync().Forget();
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
            ex.LogAsync().Forget();
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
        ApplyFilterSettings();
        LoadHighlighting();

        if (ViewModel != null)
            await ViewModel.OnLoadedAsync(null);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Applies user settings to the nodes filter
    /// </summary>
    private void ApplyFilterSettings()
    {
        try
        {
            // set the grid splitter position
            FilterSettings filterSettings = UserSettingsService.Instance.GetSetting<FilterSettings>();
            if (filterSettings is not null)
            {
                ViewModel.SelectedFilterOptions = filterSettings.OriginalValue.FilterByOptions;
                ViewModel.IsCaseSensitive = filterSettings.OriginalValue.IsCaseSensitive;
            }
        }
        catch (Exception ex)
        {
            ex.LogAsync().Forget();
        }
    }

    /// <summary>
    /// Loads XSHD markup for highlighting
    /// </summary>
    private void LoadHighlighting()
    {
        if (ViewModel is null)
            return;

        using var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Cats.Telescope.VsExtension.Resources.json.xshd");
        using var reader = new System.Xml.XmlTextReader(stream);
        ViewModel.HighlightingDefinition =
            ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(reader,
            ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance);
    }

    /// <summary>
    /// Applies user settings to the window like the grid splitter position etc.
    /// </summary>
    private async Task ApplyUISettingAsync()
    {
        try
        {
            // set the grid splitter position
            ContentGridWidthSetting gridSettings = UserSettingsService.Instance.GetSetting<ContentGridWidthSetting>();
            if (gridSettings is not null)
                TreeGridColumn.Width = new GridLength(gridSettings.OriginalValue.TreeColumnWidth);

            // set the window size
            WindowSizeSettings windowSizeSettings = UserSettingsService.Instance.GetSetting<WindowSizeSettings>();
            if (windowSizeSettings is not null)
            {
                SizeChanged -= MainWindowControl_SizeChanged;

                await ViewModel.ToolWindowPane.SetThisWindowSizeAsync((int)windowSizeSettings.OriginalValue.Width, (int)windowSizeSettings.OriginalValue.Height);

                SizeChanged += MainWindowControl_SizeChanged;
            }
        }
        catch (Exception ex)
        {
            ex.LogAsync().Forget();
        }
    }

    #endregion

    private void SearchControl_SearchStarted(object sender, string e)
    {
        if (ViewModel is null)
            return;

        ViewModel.RefreshSelected();
    }

    //private void ConfigurationTextEditor_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    //{
    //    TextEditor textEditor = (TextEditor)sender;
    //    TextView textView = textEditor.TextArea.TextView;

    //    textEditor.ScrollToVerticalOffset(textEditor.VerticalOffset - e.Delta);
    //    e.Handled = true;
    //}
}