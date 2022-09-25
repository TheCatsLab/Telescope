using Cats.Telescope.VsExtension.Mvvm.Commands;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Cats.Telescope.VsExtension.Core.Controls;

/// <summary>
/// Interaction logic for SearchControl.xaml
/// </summary>
public partial class SearchControl : UserControl
{
    private const short MaximumQueriesListSize = 5;

    public SearchControl()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        "Placeholder", typeof(string), typeof(SearchControl), new PropertyMetadata(null));

    public static readonly DependencyProperty SearchTextProperty = DependencyProperty.Register(
        "SearchText", typeof(string), typeof(SearchControl), new PropertyMetadata(new PropertyChangedCallback(OnSearchTextChanged)));

    public static readonly DependencyPropertyKey ClearSearchTextProperty = DependencyProperty.RegisterReadOnly(
        "ClearSearchText", typeof(ICommand), typeof(SearchControl), new PropertyMetadata(new RelayCommand(CanClearText, OnClearText), null));

    internal static readonly DependencyPropertyKey QueriesProperty = DependencyProperty.RegisterReadOnly(
        "Queries", typeof(ObservableCollection<string>), typeof(SearchControl), new PropertyMetadata(new ObservableCollection<string>(), null));

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public string SearchText
    {
        get => (string)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public ObservableCollection<string> Queries =>
        (ObservableCollection<string>)GetValue(QueriesProperty.DependencyProperty);

    public ICommand ClearSearchTextCommand =>
        (ICommand)GetValue(ClearSearchTextProperty.DependencyProperty);

    public static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        SearchControl searchControl = d as SearchControl;
        string text = d.GetValue(SearchTextProperty) as string;

        searchControl.AddSearchHistory(text);
    }

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
        if (Queries.Any(q => q.Equals(text, StringComparison.InvariantCultureIgnoreCase)))
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

    private void PART_FilterPlaceholder_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        PART_FilterTextBox.Focus();
        //FocusManager.SetFocusedElement(this, PART_FilterTextBox);
    }
}
