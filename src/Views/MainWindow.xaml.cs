using Cats.Telescope.VsExtension.Core;
using Cats.Telescope.VsExtension.Core.Controls.AvalonEdit;
using Cats.Telescope.VsExtension.Core.Extensions;
using Cats.Telescope.VsExtension.Core.Settings;
using Cats.Telescope.VsExtension.Core.Utils;
using Cats.Telescope.VsExtension.ViewModels;
using ICSharpCode.AvalonEdit.Document;
using Microsoft.Xaml.Behaviors;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        // Ctrl+E focuses the search box, the standard tool window search shortcut
        this.PreviewKeyDown += (sender, e) =>
        {
            if (e.Key == Key.E && Keyboard.Modifiers == ModifierKeys.Control)
            {
                TelescopeSearchControl.FocusInput();
                e.Handled = true;
            }
        };

        ConfigurationTextEditor.TextArea.TextView.LineTransformers.Add(new ColorizeAvalonEdit());

        // enable the standard AvalonEdit find-in-document panel (Ctrl+F)
        ICSharpCode.AvalonEdit.Search.SearchPanel.Install(ConfigurationTextEditor);

        // keep the match marks strip in sync with the document and the search state
        ConfigurationTextEditor.TextChanged += (sender, e) => RefreshMatchMarks();
        MatchMarksCanvas.SizeChanged += (sender, e) => RefreshMatchMarks();
        viewModel.PropertyChanged += ViewModel_PropertyChanged;

        viewModel.CopiedToClipboard += ViewModel_CopiedToClipboard;
        Loaded += MainWindowControl_Loaded;

        ContentGridSplitter.DragCompleted += ContentGridSplitter_DragCompleted;
        SizeChanged += MainWindowControl_SizeChanged;
        _resizeTimer.Tick += ResizeTimer_Tick;

        viewModel.FilterSettingsChanged += ViewModel_FilterSettingsChanged;

        // close the filter options popup on Esc
        FilterOptionsPopup.KeyDown += (sender, e) =>
        {
            if (e.Key == Key.Escape)
                ViewModel?.ToggleFilterOptionsVisibilityCommand.Execute(false);
        };
    }

    private MainWindowViewModel ViewModel => DataContext as MainWindowViewModel;

    /// <summary>
    /// Refreshes the match marks strip when the search state or the selected node changes
    /// </summary>
    private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.SearchText)
            or nameof(MainWindowViewModel.IsCaseSensitive)
            or nameof(MainWindowViewModel.UseRegex)
            or nameof(MainWindowViewModel.SelectedNode))
        {
            RefreshMatchMarks();
        }
    }

    /// <summary>
    /// Draws a small mark on the strip next to the scrollbar for every search hit in the document;
    /// clicking a mark scrolls the editor to the hit
    /// </summary>
    private void RefreshMatchMarks()
    {
        MatchMarksCanvas.Children.Clear();

        MainWindowViewModel viewModel = ViewModel;
        ICSharpCode.AvalonEdit.Document.TextDocument document = ConfigurationTextEditor?.Document;

        if (viewModel is null || document is null || string.IsNullOrEmpty(viewModel.SearchText))
            return;

        string pattern = viewModel.UseRegex ? viewModel.SearchText : System.Text.RegularExpressions.Regex.Escape(viewModel.SearchText);

        System.Text.RegularExpressions.RegexOptions regexOptions = viewModel.IsCaseSensitive
            ? System.Text.RegularExpressions.RegexOptions.None
            : System.Text.RegularExpressions.RegexOptions.IgnoreCase;

        System.Text.RegularExpressions.MatchCollection matches;

        try
        {
            matches = System.Text.RegularExpressions.Regex.Matches(document.Text, pattern, regexOptions);
        }
        catch (System.ArgumentException)
        {
            // a broken regex in the regex mode - nothing to mark
            return;
        }

        double usableHeight = Math.Max(1, MatchMarksCanvas.ActualHeight - 3);
        int totalLines = Math.Max(1, document.LineCount);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            int lineNumber = document.GetLineByOffset(match.Index).LineNumber;

            System.Windows.Shapes.Rectangle mark = new()
            {
                Width = 5,
                Height = 3,
                RadiusX = 1,
                RadiusY = 1,
                Fill = SystemColors.HighlightBrush,
                Cursor = Cursors.Hand,
                ToolTip = match.Value,
                Tag = match.Index
            };

            Canvas.SetTop(mark, Math.Min(lineNumber / (double)totalLines * usableHeight, usableHeight));
            Canvas.SetLeft(mark, 0);

            mark.MouseLeftButtonDown += MatchMark_Click;

            MatchMarksCanvas.Children.Add(mark);
        }
    }

    /// <summary>
    /// Scrolls the editor to the hit behind the clicked mark
    /// </summary>
    private void MatchMark_Click(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is int offset)
        {
            ConfigurationTextEditor.Select(offset, 0);
            ICSharpCode.AvalonEdit.Document.TextLocation location = ConfigurationTextEditor.Document.GetLocation(offset);
            ConfigurationTextEditor.ScrollToLine(location.Line);
            ConfigurationTextEditor.Focus();
        }
    }

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
            // copied straight from a list item(e.g. an environment variable) - the pane header is a neutral anchor
            _ => CopyNameToClipboardButton
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
        try
        {
            if (ViewModel is null)
                return;

            using var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Cats.Telescope.VsExtension.Resources.json.xshd");
            using var reader = new System.Xml.XmlTextReader(stream);
            ViewModel.HighlightingDefinition =
                ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(reader,
                ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance);
        }
        catch (Exception ex)
        {
            ex.LogAsync().Forget();
        }
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
}