using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Cats.Telescope.VsExtension.Core.Controls;

public static class TextBlockHighlighter
{
    public static string GetSelection(DependencyObject obj)
    {
        return (string)obj.GetValue(SelectionProperty);
    }

    public static void SetSelection(DependencyObject obj, string value)
    {
        obj.SetValue(SelectionProperty, value);
    }

    public static readonly DependencyProperty SelectionProperty =
        DependencyProperty.RegisterAttached("Selection", typeof(string), typeof(TextBlockHighlighter),
            new PropertyMetadata(new PropertyChangedCallback(SelectText)));

    public static readonly DependencyProperty HighlightColorProperty =
        DependencyProperty.RegisterAttached("HighlightColor", typeof(Brush), typeof(TextBlockHighlighter),
            new PropertyMetadata(Brushes.Yellow, new PropertyChangedCallback(SelectText)));

    public static readonly DependencyProperty ForecolorProperty =
        DependencyProperty.RegisterAttached("Forecolor", typeof(Brush), typeof(TextBlockHighlighter),
            new PropertyMetadata(Brushes.Black, new PropertyChangedCallback(SelectText)));

    public static readonly DependencyProperty StringComparisonProperty =
        DependencyProperty.RegisterAttached("StringComparison", typeof(StringComparison), typeof(TextBlockHighlighter), new PropertyMetadata(StringComparison.CurrentCultureIgnoreCase, OnComparisonChanged));

    private static void OnComparisonChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        SelectText(d, e);
    }

    private static void SelectText(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is null) 
            return;

        if (d is not TextBlock) 
            throw new InvalidOperationException("Only valid for TextBlock");

        TextBlock txtBlock = d as TextBlock;
        string text = txtBlock.Text;
        if (string.IsNullOrEmpty(text)) 
            return;

        txtBlock.Inlines.Clear();

        string highlightText = (string)d.GetValue(SelectionProperty);
        if (string.IsNullOrEmpty(highlightText))
        {
            txtBlock.Inlines.Add(new Run(text));
            return;
        }

        StringComparison stringComparison = GetStringComparison(d);

        int index = text.IndexOf(highlightText, stringComparison);
        if (index < 0)
        {
            txtBlock.Inlines.Add(new Run(text));
            return;
        }

        Brush selectionColor = (Brush)d.GetValue(HighlightColorProperty);
        Brush forecolor = (Brush)d.GetValue(ForecolorProperty);

        while (true)
        {
            txtBlock.Inlines.AddRange(new Inline[] {
                new Run(text.Substring(0, index)),
                new Run(text.Substring(index, highlightText.Length)) {Background = selectionColor,
                    Foreground = forecolor}
            });

            text = text.Substring(index + highlightText.Length);
            index = text.IndexOf(highlightText, stringComparison);

            if (index < 0)
            {
                txtBlock.Inlines.Add(new Run(text));
                break;
            }
        }
    }

    public static StringComparison GetStringComparison(DependencyObject obj)
    {
        return (StringComparison)obj.GetValue(StringComparisonProperty);
    }

    public static void SetStringComparison(DependencyObject obj, StringComparison value)
    {
        obj.SetValue(StringComparisonProperty, value);
    }

    public static Brush GetHighlightColor(DependencyObject obj)
    {
        return (Brush)obj.GetValue(HighlightColorProperty);
    }

    public static void SetHighlightColor(DependencyObject obj, Brush value)
    {
        obj.SetValue(HighlightColorProperty, value);
    }

    public static Brush GetForecolor(DependencyObject obj)
    {
        return (Brush)obj.GetValue(ForecolorProperty);
    }

    public static void SetForecolor(DependencyObject obj, Brush value)
    {
        obj.SetValue(ForecolorProperty, value);
    }

}
