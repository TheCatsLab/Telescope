using Cats.Telescope.VsExtension.Core;
using Cats.Telescope.VsExtension.Core.Controls.AvalonEdit;
using Cats.Telescope.VsExtension.Core.Extensions;
using Cats.Telescope.VsExtension.Core.Settings;
using Cats.Telescope.VsExtension.Core.Utils;
using Cats.Telescope.VsExtension.ViewModels;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using Microsoft.Xaml.Behaviors;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Cats.Telescope.VsExtension.Views;

/// <summary>
/// Interaction logic for MainWindowControl.
/// </summary>
public partial class MainWindowControl : UserControl, IDisposable
{
    private readonly DispatcherTimer _resizeTimer = new() { Interval = new TimeSpan(0, 0, 0, 0, TelescopeConstants.Window.ResizeTimerInterval), IsEnabled = false };

    private bool _isDisposed;
    private bool _hasLoaded;
    private bool _loadingCancelledOnUnload;

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
        this.PreviewKeyDown += MainWindowControl_PreviewKeyDown;

        // enable the standard AvalonEdit find-in-document panel (Ctrl+F)
        ICSharpCode.AvalonEdit.Search.SearchPanel.Install(ConfigurationTextEditor);

        ConfigurationTextEditor.TextArea.TextView.LineTransformers.Add(new ColorizeAvalonEdit());
        _matchHighlightTransformer = new MatchHighlightTransformer();
        ConfigurationTextEditor.TextArea.TextView.LineTransformers.Add(_matchHighlightTransformer);

        // keep the match marks strips and the hit highlighting in sync with the search state;
        // named handlers are required to detach them when the tool window is closed
        ConfigurationTextEditor.TextChanged += ConfigurationTextEditor_TextChanged;
        ConfigurationTextEditor.TextArea.DocumentChanged += ConfigurationTextEditorTextArea_DocumentChanged;
        MatchMarksCanvas.SizeChanged += MatchMarksCanvas_SizeChanged;
        TagMatchMarksCanvas.SizeChanged += TagMatchMarksCanvas_SizeChanged;
        EnvMatchMarksCanvas.SizeChanged += EnvMatchMarksCanvas_SizeChanged;
        viewModel.PropertyChanged += ViewModel_PropertyChanged;

        viewModel.CopiedToClipboard += ViewModel_CopiedToClipboard;
        Loaded += MainWindowControl_Loaded;
        Unloaded += MainWindowControl_Unloaded;

        ContentGridSplitter.DragCompleted += ContentGridSplitter_DragCompleted;
        SizeChanged += MainWindowControl_SizeChanged;
        _resizeTimer.Tick += ResizeTimer_Tick;

        viewModel.FilterSettingsChanged += ViewModel_FilterSettingsChanged;

        // close the filter options popup on Esc
        FilterOptionsPopup.KeyDown += FilterOptionsPopup_KeyDown;
    }

    /// <summary>
    /// Detaches every handler and releases the view model. The tool window pane calls
    /// Dispose on its Content, so the whole control graph becomes collectible on close.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        _resizeTimer.Stop();
        ClearSearchHighlights();

        PreviewKeyDown -= MainWindowControl_PreviewKeyDown;
        ConfigurationTextEditor.TextChanged -= ConfigurationTextEditor_TextChanged;
        ConfigurationTextEditor.TextArea.DocumentChanged -= ConfigurationTextEditorTextArea_DocumentChanged;
        MatchMarksCanvas.SizeChanged -= MatchMarksCanvas_SizeChanged;
        TagMatchMarksCanvas.SizeChanged -= TagMatchMarksCanvas_SizeChanged;
        EnvMatchMarksCanvas.SizeChanged -= EnvMatchMarksCanvas_SizeChanged;
        ContentGridSplitter.DragCompleted -= ContentGridSplitter_DragCompleted;
        SizeChanged -= MainWindowControl_SizeChanged;
        _resizeTimer.Tick -= ResizeTimer_Tick;
        FilterOptionsPopup.KeyDown -= FilterOptionsPopup_KeyDown;
        Loaded -= MainWindowControl_Loaded;
        Unloaded -= MainWindowControl_Unloaded;

        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            viewModel.CopiedToClipboard -= ViewModel_CopiedToClipboard;
            viewModel.FilterSettingsChanged -= ViewModel_FilterSettingsChanged;
            viewModel.Dispose();
        }

        DataContext = null;
    }

    private MainWindowViewModel ViewModel => DataContext as MainWindowViewModel;

    /// <summary>
    /// An offset-based search hit; keeps only the small tooltip text instead of the
    /// regex Match, which retains a full copy of the document text(a LOH entry)
    /// </summary>
    private readonly struct MarkSpan
    {
        public MarkSpan(int index, int length, string value)
        {
            Index = index;
            Length = length;
            Value = value;
        }

        public int Index { get; }
        public int Length { get; }
        public string Value { get; }
    }

    /// <summary>
    /// Paints the yellow background behind the search hits of the configuration editor;
    /// a visual line transformer is used since it is the same proven pipeline as the syntax highlighting
    /// </summary>
    private sealed class MatchHighlightTransformer : DocumentColorizingTransformer
    {
        private Brush _brush;
        private System.Collections.Generic.IReadOnlyList<MarkSpan> _matches;

        public void SetMatches(Brush brush, System.Collections.Generic.IReadOnlyList<MarkSpan> matches)
        {
            _brush = brush;
            _matches = matches;
        }

        protected override void ColorizeLine(ICSharpCode.AvalonEdit.Document.DocumentLine line)
        {
            if (_brush is null || _matches is null || _matches.Count == 0)
                return;

            int lineStart = line.Offset;
            int lineEnd = line.EndOffset;

            foreach (MarkSpan match in _matches)
            {
                if (match.Index >= lineEnd || match.Index + match.Length <= lineStart)
                    continue;

                int start = Math.Max(match.Index, lineStart);
                int end = Math.Min(match.Index + match.Length, lineEnd);

                ChangeLinePart(start, end, element => element.TextRunProperties.SetBackgroundBrush(_brush));
            }
        }
    }

    private MatchHighlightTransformer _matchHighlightTransformer;

    // cached search hits of the current query over the configuration document as offsets;
    // document.Text copies the whole text and Regex.Matches scans all of it, so both are
    // redone only when the document, the pattern or the case/regex switches actually change,
    // and only the small spans are kept afterwards - never the regex objects
    private ICSharpCode.AvalonEdit.Document.ITextSourceVersion _marksDocumentVersion;
    private string _marksPattern;
    private System.Text.RegularExpressions.RegexOptions _marksRegexOptions;
    private System.Collections.Generic.List<MarkSpan> _marksSpans;
    private bool _marksCacheValid;

    // the span list currently applied to the highlight transformer;
    // when it is unchanged, the transformer state and the redraw are skipped
    private System.Collections.Generic.IReadOnlyList<MarkSpan> _appliedHighlightSpans;

    /// <summary>
    /// Refreshes the match marks strips when the search state or the selected node changes
    /// </summary>
    private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.SearchText)
            or nameof(MainWindowViewModel.IsCaseSensitive)
            or nameof(MainWindowViewModel.UseRegex)
            or nameof(MainWindowViewModel.SelectedNode))
        {
            RefreshMatchMarks();
            RefreshTagMatchMarks();
            RefreshEnvMatchMarks();
        }
    }

    /// <summary>
    /// Returns the search pattern for the current query honoring the literal/regex and case switches
    /// </summary>
    private static string GetSearchPattern(MainWindowViewModel viewModel, out System.Text.RegularExpressions.RegexOptions regexOptions)
    {
        regexOptions = viewModel.IsCaseSensitive
            ? System.Text.RegularExpressions.RegexOptions.None
            : System.Text.RegularExpressions.RegexOptions.IgnoreCase;

        return viewModel.UseRegex ? viewModel.SearchText : System.Text.RegularExpressions.Regex.Escape(viewModel.SearchText);
    }

    /// <summary>
    /// Draws a small mark on the strip next to the scrollbar for every search hit in the document,
    /// and highlights the hits inside the editor;
    /// clicking a mark scrolls the editor to the hit and selects it
    /// </summary>
    private void RefreshMatchMarks()
    {
        MatchMarksCanvas.Children.Clear();

        MainWindowViewModel viewModel = ViewModel;
        ICSharpCode.AvalonEdit.Document.TextDocument document = ConfigurationTextEditor?.Document;

        if (viewModel is null || document is null || string.IsNullOrEmpty(viewModel.SearchText))
        {
            InvalidateMarksCache();
            _appliedHighlightSpans = null;
            _matchHighlightTransformer?.SetMatches(null, null);
            ConfigurationTextEditor?.TextArea.TextView.Redraw();
            return;
        }

        string pattern = GetSearchPattern(viewModel, out System.Text.RegularExpressions.RegexOptions regexOptions);

        System.Collections.Generic.IReadOnlyList<MarkSpan> matches = GetCachedMatches(document, pattern, regexOptions);

        if (matches is null)
        {
            // a broken regex in the regex mode - nothing to mark
            _appliedHighlightSpans = null;
            _matchHighlightTransformer?.SetMatches(null, null);
            ConfigurationTextEditor.TextArea.TextView.Redraw();
            return;
        }

        // the yellow background behind the hits inside the editor;
        // redraw only when the highlighter state actually changed
        if (!ReferenceEquals(_appliedHighlightSpans, matches))
        {
            _appliedHighlightSpans = matches;
            _matchHighlightTransformer?.SetMatches((Brush)FindResource("HighlightBrush"), matches);
            ConfigurationTextEditor.TextArea.TextView.Redraw();
        }

        double usableHeight = Math.Max(1, MatchMarksCanvas.ActualHeight - 3);
        int totalLines = Math.Max(1, document.LineCount);
        Brush markBrush = (Brush)FindResource("HighlightBrush");

        foreach (MarkSpan match in matches)
        {
            int lineNumber = document.GetLineByOffset(match.Index).LineNumber;

            System.Windows.Shapes.Rectangle mark = new()
            {
                Width = 5,
                Height = 3,
                RadiusX = 1,
                RadiusY = 1,
                Fill = markBrush,
                Cursor = Cursors.Hand,
                ToolTip = match.Value,
                Tag = match
            };

            Canvas.SetTop(mark, Math.Min(lineNumber / (double)totalLines * usableHeight, usableHeight));
            Canvas.SetLeft(mark, 0);

            mark.MouseLeftButtonDown += MatchMark_Click;

            MatchMarksCanvas.Children.Add(mark);
        }
    }

    /// <summary>
    /// Returns the search hits of the current query as offsets, reusing the cached spans unless
    /// the document, the pattern or the options have changed since the previous refresh;
    /// returns null when the pattern is not a valid regular expression
    /// </summary>
    private System.Collections.Generic.IReadOnlyList<MarkSpan> GetCachedMatches(ICSharpCode.AvalonEdit.Document.TextDocument document, string pattern, System.Text.RegularExpressions.RegexOptions regexOptions)
    {
        // the document version instance changes on every edit, so reference equality
        // means "no edits since the cached hits were calculated"
        bool isCacheValid = _marksCacheValid
            && _marksPattern == pattern
            && _marksRegexOptions == regexOptions
            && document.Version is not null
            && ReferenceEquals(document.Version, _marksDocumentVersion);

        if (isCacheValid)
            return _marksSpans;

        try
        {
            List<MarkSpan> spans = new();

            // the match collection (and the full text copy behind it) is alive only inside
            // this loop - the spans keep just the small per-hit values
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(document.Text, pattern, regexOptions))
                spans.Add(new MarkSpan(match.Index, match.Length, match.Value));

            _marksSpans = spans;
            _marksPattern = pattern;
            _marksRegexOptions = regexOptions;
            _marksDocumentVersion = document.Version;
            _marksCacheValid = true;
        }
        catch (System.ArgumentException)
        {
            InvalidateMarksCache();
            return null;
        }

        return _marksSpans;
    }

    /// <summary>
    /// Drops the cached document hits
    /// </summary>
    private void InvalidateMarksCache()
    {
        _marksCacheValid = false;
        _marksSpans = null;
        _marksDocumentVersion = null;
        _marksPattern = null;
    }

    /// <summary>
    /// Scrolls the editor to the hit behind the clicked mark and selects it
    /// </summary>
    private void MatchMark_Click(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is MarkSpan match)
        {
            ConfigurationTextEditor.Select(match.Index, match.Length);
            ICSharpCode.AvalonEdit.Document.TextLocation location = ConfigurationTextEditor.Document.GetLocation(match.Index);
            ConfigurationTextEditor.ScrollToLine(location.Line);
            ConfigurationTextEditor.Focus();
        }
    }

    /// <summary>
    /// Refreshes the match marks strip of the Tags list
    /// </summary>
    private void RefreshTagMatchMarks()
    {
        RefreshListMatchMarks(TagMatchMarksCanvas, TagsListView, ViewModel?.SelectedNode?.Tags);
    }

    /// <summary>
    /// Refreshes the match marks strip of the Environment Variables list
    /// </summary>
    private void RefreshEnvMatchMarks()
    {
        RefreshListMatchMarks(EnvMatchMarksCanvas, EnvVarsListView, ViewModel?.SelectedNode?.EnvironmentVariables);
    }

    /// <summary>
    /// Draws the match marks for a key/value list(Tags or Environment variables);
    /// the marks are positioned by the item index, clicking a mark scrolls the list to the matching item
    /// </summary>
    private void RefreshListMatchMarks(Canvas canvas, System.Windows.Controls.ListBox list, System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, string>> items)
    {
        canvas.Children.Clear();

        MainWindowViewModel viewModel = ViewModel;

        if (viewModel is null || items is null || string.IsNullOrEmpty(viewModel.SearchText))
            return;

        string pattern = GetSearchPattern(viewModel, out System.Text.RegularExpressions.RegexOptions regexOptions);

        // the items are concatenated with newlines - the same way the filter matcher sees them
        System.Text.StringBuilder text = new();
        var ranges = new System.Collections.Generic.List<(int itemIndex, int start, int end)>();

        int position = 0;
        int itemIndex = 0;

        foreach (var item in items)
        {
            int start = position;

            text.Append(item.Key ?? string.Empty);
            position += item.Key?.Length ?? 0;
            text.Append('\n');
            position++;
            text.Append(item.Value ?? string.Empty);
            position += item.Value?.Length ?? 0;

            ranges.Add((itemIndex, start, position));

            text.Append('\n');
            position++;
            itemIndex++;
        }

        System.Text.RegularExpressions.MatchCollection matches;

        try
        {
            matches = System.Text.RegularExpressions.Regex.Matches(text.ToString(), pattern, regexOptions);
        }
        catch (System.ArgumentException)
        {
            return;
        }

        double usableHeight = Math.Max(1, canvas.ActualHeight - 3);
        int totalItems = Math.Max(1, ranges.Count);
        Brush markBrush = (Brush)FindResource("HighlightBrush");

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            int ownerIndex = 0;

            for (int i = 0; i < ranges.Count; i++)
            {
                if (ranges[i].start <= match.Index)
                    ownerIndex = ranges[i].itemIndex;
                else
                    break;
            }

            System.Windows.Shapes.Rectangle mark = new()
            {
                Width = 5,
                Height = 3,
                RadiusX = 1,
                RadiusY = 1,
                Fill = markBrush,
                Cursor = Cursors.Hand,
                ToolTip = match.Value,
            };

            Canvas.SetTop(mark, Math.Min((ownerIndex + 0.5) / totalItems * usableHeight, usableHeight));
            Canvas.SetLeft(mark, 0);

            int capturedIndex = ownerIndex;
            mark.MouseLeftButtonDown += (sender, args) => list.ScrollIntoView(list.Items[capturedIndex]);

            canvas.Children.Add(mark);
        }
    }

    #region Event handlers

    private void MainWindowControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.E && Keyboard.Modifiers == ModifierKeys.Control)
        {
            TelescopeSearchControl.FocusInput();
            e.Handled = true;
        }
    }

    private void ConfigurationTextEditor_TextChanged(object sender, EventArgs e) => RefreshMatchMarks();

    /// <summary>
    /// Recalculates the marks when the editor document is replaced: the document binding
    /// swaps in the full JSON when the selected node's data materializes, and AvalonEdit
    /// does not raise TextChanged on a document swap - without this the marks and the
    /// highlighter stay computed against the pre-materialization document
    /// </summary>
    private void ConfigurationTextEditorTextArea_DocumentChanged(object sender, EventArgs e) => RefreshMatchMarks();

    private void MatchMarksCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => RefreshMatchMarks();

    private void TagMatchMarksCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => RefreshTagMatchMarks();

    private void EnvMatchMarksCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => RefreshEnvMatchMarks();

    private void FilterOptionsPopup_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            ViewModel?.ToggleFilterOptionsVisibilityCommand.Execute(false);
    }

    /// <summary>
    /// Stops the work that would keep the control alive while it is detached from the
    /// window: the tool window is hidden/re-docked on every auto-hide toggle, so this
    /// runs much more often than Dispose
    /// </summary>
    private void MainWindowControl_Unloaded(object sender, RoutedEventArgs e)
    {
        // a running DispatcherTimer is rooted by the dispatcher and holds this control
        // through its Tick handler - stop it on unload so the control does not stay
        // alive until the next tick when the tool window is closed during a resize
        _resizeTimer.Stop();

        // in-flight loadings keep the tree and the Azure SDK resources alive through
        // their async continuations; they are restarted on the next Loaded if needed
        if (ViewModel?.IsBusy == true)
        {
            _loadingCancelledOnUnload = true;
            ViewModel.CancelLoading();
        }

        ClearSearchHighlights();
    }

    private void MainWindowControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isDisposed)
            return;

        _resizeTimer.IsEnabled = true;
        _resizeTimer.Stop();
        _resizeTimer.Start();
    }

    /// <summary>
    /// Drops the cached search matches and the highlights; the match collection keeps
    /// a copy of the whole document text alive, which can be a large object heap entry
    /// </summary>
    private void ClearSearchHighlights()
    {
        InvalidateMarksCache();
        _appliedHighlightSpans = null;
        _matchHighlightTransformer?.SetMatches(null, null);
        MatchMarksCanvas.Children.Clear();
        TagMatchMarksCanvas.Children.Clear();
        EnvMatchMarksCanvas.Children.Clear();
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

        if (_isDisposed)
            return;

        ClipboardPopup.IsOpen = false;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "Async event handler")]
    private async void MainWindowControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isDisposed)
            return;

        if (_hasLoaded)
        {
            // Loaded fires again when the tool window is shown after auto-hide or a
            // re-dock; do not reload the whole tree unless the previous load was
            // cancelled on unload and left it empty
            if (_loadingCancelledOnUnload && ViewModel?.ResourceNodes.Count == 0)
            {
                _loadingCancelledOnUnload = false;
                await ViewModel.OnLoadedAsync(null);
            }

            return;
        }

        _hasLoaded = true;
        _loadingCancelledOnUnload = false;

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
        if (_isDisposed)
            return;

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

                if (!_isDisposed)
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