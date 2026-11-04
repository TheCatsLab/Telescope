using Cats.Telescope.VsExtension.Core.Enums;
using Cats.Telescope.VsExtension.ViewModels;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using System;
using System.Windows;
using System.Windows.Media;

namespace Cats.Telescope.VsExtension.Core.Controls.AvalonEdit;

public class ColorizeAvalonEdit : DocumentColorizingTransformer
{
    // allocated once and frozen - a per-match BrushConverter plus a new brush
    // repeated for every hit of every rendered line on every redraw
    private static readonly SolidColorBrush MatchBackgroundBrush = CreateMatchBackgroundBrush();

    // the highlighted typeface derives from the element's family and stretch;
    // the editor renders with a single family in practice, so a single-entry cache suffices
    private static FontFamily _cachedFontFamily;
    private static FontStretch _cachedFontStretch;
    private static Typeface _cachedTypeface;

    private static SolidColorBrush CreateMatchBackgroundBrush()
    {
        SolidColorBrush brush = new(Color.FromRgb(0xFF, 0xFF, 0x00));
        brush.Freeze();
        return brush;
    }

    private static Typeface GetHighlightTypeface(FontFamily fontFamily, FontStretch stretch)
    {
        if (!Equals(fontFamily, _cachedFontFamily) || stretch != _cachedFontStretch)
        {
            _cachedTypeface = new Typeface(fontFamily, FontStyles.Italic, FontWeights.Bold, stretch);
            _cachedFontFamily = fontFamily;
            _cachedFontStretch = stretch;
        }

        return _cachedTypeface;
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        if (CurrentContext.TextView.DataContext is not MainWindowViewModel viewModel)
            return;

        if (!viewModel.SelectedFilterOptions.HasFlag(FilterBy.ResourceData))
            return;

        string searchText = viewModel.SearchText;

        if (string.IsNullOrEmpty(searchText))
            return;

        StringComparison comparisonType = viewModel.IsCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        int lineStartOffset = line.Offset;
        string text = CurrentContext.Document.GetText(line);
        int start = 0;
        int index;

        while ((index = text.IndexOf(searchText, start, comparisonType)) >= 0)
        {
            base.ChangeLinePart(
                lineStartOffset + index, // startOffset
                lineStartOffset + index + searchText.Length, // endOffset
                (VisualLineElement element) => {
                    // This lambda gets called once for every VisualLineElement
                    // between the specified offsets.

                    // Replace the typeface with a modified version of
                    // the same typeface
                    Typeface tf = element.TextRunProperties.Typeface;
                    element.TextRunProperties.SetTypeface(GetHighlightTypeface(tf.FontFamily, tf.Stretch));
                    element.TextRunProperties.SetBackgroundBrush(MatchBackgroundBrush);
                });
            start = index + 1; // search for next occurrence
        }
    }
}
