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
                    Typeface tf = element.TextRunProperties.Typeface;

                    // Replace the typeface with a modified version of
                    // the same typeface
                    element.TextRunProperties.SetTypeface(new Typeface(
                        tf.FontFamily,
                        FontStyles.Italic,
                        FontWeights.Bold,
                        tf.Stretch
                    ));

                    element.TextRunProperties.SetBackgroundBrush((SolidColorBrush)new BrushConverter().ConvertFrom("#FFFF00"));
                });
            start = index + 1; // search for next occurrence
        }
    }
}
