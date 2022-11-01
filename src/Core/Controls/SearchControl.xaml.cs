using Cats.Telescope.VsExtension.Core.Extensions;
using Cats.Telescope.VsExtension.Mvvm.Commands;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Cats.Telescope.VsExtension.Core.Controls;

public partial class SearchControl : UserControl
{
    private const short MaximumQueriesListSize = 5;
    private DispatcherTimer _searchTimer;

    public SearchControl()
    {
        InitializeComponent();

        this.KeyDown += SearchControl_KeyDown;
    }

    #region Placeholder

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        "Placeholder", typeof(string), typeof(SearchControl), new PropertyMetadata(null));

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    #endregion

    #region SearchText

    public static readonly DependencyProperty SearchTextProperty = DependencyProperty.Register(
        "SearchText", typeof(string), typeof(SearchControl), new PropertyMetadata(new PropertyChangedCallback(OnSearchTextChanged)));

    public string SearchText
    {
        get => (string)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not SearchControl searchControl)
            return;

        searchControl.InvokeSearch();
    }

    #endregion

    #region History

    internal static readonly DependencyPropertyKey QueriesProperty = DependencyProperty.RegisterReadOnly(
        "Queries", typeof(ObservableCollection<string>), typeof(SearchControl), new PropertyMetadata(new ObservableCollection<string>(), null));

    public ObservableCollection<string> Queries =>
        (ObservableCollection<string>)GetValue(QueriesProperty.DependencyProperty);

    /// <summary>
    /// Adds string to the seach history
    /// </summary>
    /// <param name="text"></param>
    private void AddSearchHistory(string text)
    {
        // avoid adding empty strings
        if (string.IsNullOrWhiteSpace(text))
            return;

        // avoid adding duplicates
        if (Queries.Any(q => q.Equals(text, StringComparison.OrdinalIgnoreCase)))
            return;

        Queries.Add(text);

        AdjustHistoryLength();
    }

    /// <summary>
    /// Clears the search history
    /// </summary>
    private void AdjustHistoryLength()
    {
        if (Queries is not null && Queries.Count() > MaximumQueriesListSize)
        {
            while (Queries.Count() > MaximumQueriesListSize)
            {
                Queries.RemoveAt(0);
            }
        }
    }
    #endregion

    #region UseCaseOption

    internal static readonly DependencyProperty UseCaseOptionProperty = DependencyProperty.Register(
        "UseCaseOption", typeof(bool), typeof(SearchControl), null);

    public bool UseCaseOption
    {
        get => (bool)GetValue(UseCaseOptionProperty);
        set => SetValue(UseCaseOptionProperty, value);
    }

    #endregion

    #region EnterKeyDefaut

    internal static readonly DependencyProperty EnterKeyDefaultProperty = DependencyProperty.Register(
        "EnterKeyDefault", typeof(bool), typeof(SearchControl), null);

    public bool EnterKeyDefault
    {
        get => (bool)GetValue(EnterKeyDefaultProperty);
        set => SetValue(EnterKeyDefaultProperty, value); 
    }

    #endregion

    #region IsCaseSensitive

    public static readonly DependencyProperty IsCaseSensitiveProperty = DependencyProperty.Register(
        "IsCaseSensitive", typeof(bool), typeof(SearchControl), new PropertyMetadata(false, OnIsCaseSensitiveChanged));

    public bool IsCaseSensitive
    {
        get => (bool)GetValue(IsCaseSensitiveProperty);
        set => SetValue(IsCaseSensitiveProperty, value);
    }

    private static void OnIsCaseSensitiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if(d is SearchControl searchControl)
        {
            searchControl.InvokeSearch(true);
        }
    }

    #endregion

    #region ClearSearchText command

    public static readonly DependencyPropertyKey ClearSearchTextProperty = DependencyProperty.RegisterReadOnly(
        "ClearSearchText", typeof(ICommand), typeof(SearchControl), new PropertyMetadata(new RelayCommand(CanClearText, OnClearText), null));

    public ICommand ClearSearchTextCommand =>
        (ICommand)GetValue(ClearSearchTextProperty.DependencyProperty);

    /// <summary>
    /// Clears the search text
    /// </summary>
    /// <param name="sender"></param>
    private static void OnClearText(object sender)
    {
        SearchControl searchControl = sender as SearchControl;

        if (searchControl is not null)
            searchControl.SearchText = string.Empty;
    }

    private static bool CanClearText(object obj) => true;

    #endregion

    #region Filter command

    public static DependencyProperty FilterCommandProperty = DependencyProperty.Register(
        "FilterCommand", typeof(ICommand), typeof(SearchControl), null);

    /// <summary>
    /// Command which performs searching/filtering
    /// </summary>
    public ICommand FilterCommand
    {
        get => (ICommand)GetValue(FilterCommandProperty);
        set => SetValue(FilterCommandProperty, value);
    }

    #endregion

    #region Delay

    public static DependencyProperty DelayProperty = DependencyProperty.Register(
        "Delay", typeof(int), typeof(SearchControl), new PropertyMetadata(0, new PropertyChangedCallback(OnDelayChanged)));

    /// <summary>
    /// Number of milliseconds to wait before searching
    /// </summary>
    public int Delay
    {
        get => (int)GetValue(DelayProperty);
        set => SetValue(DelayProperty, value);
    }

    private static void OnDelayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not SearchControl searchControl)
            return;

        int delay = (int)e.NewValue;
        searchControl.InitializeTimer(delay);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Removes the timer if exists and creates a new one
    /// </summary>
    /// <param name="delay"></param>
    private void InitializeTimer(int delay)
    {
        if (_searchTimer is not null)
        {
            _searchTimer.Stop();
            _searchTimer.Tick -= SearchTimer_Elapsed;
        }

        if(delay > 0)
        {
            _searchTimer = new()
            {
                Interval = TimeSpan.FromMilliseconds(delay)
            };
            _searchTimer.Tick += SearchTimer_Elapsed;
        }
    }

    private void SearchTimer_Elapsed(object sender, EventArgs e)
    {
        Search(SearchText);
    }

    /// <summary>
    /// Triggers the search considering timer
    /// </summary>
    private void InvokeSearch(bool immediately = false)
    {
        if (_searchTimer != null && !immediately)
            _searchTimer.Reset();
        else
            Search(SearchText);        
    }

    /// <summary>
    /// Performs the search considering <paramref name="text"/>
    /// </summary>
    /// <param name="text">query text to search for</param>
    private void Search(string text)
    {
        _searchTimer.Stop();

        FilterOptions options = new()
        {
            QueryText = text,
            StringComparison = IsCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase
        };

        if (FilterCommand != null && FilterCommand.CanExecute(options))
        {
            FilterCommand.Execute(options);
        }

        AddSearchHistory(text);
    }

    #endregion

    #region Events

    private void SearchControl_KeyDown(object sender, KeyEventArgs e)
    {
        if (EnterKeyDefault && e.Key == Key.Enter)
        {
            InvokeSearch(true);
        }
    }

    private void PART_FilterPlaceholder_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        PART_FilterTextBox.Focus();
    }

    #endregion
}
