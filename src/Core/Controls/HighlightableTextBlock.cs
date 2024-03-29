﻿using System;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Cats.Telescope.VsExtension.Core.Controls;

internal class HighlightableTextBlock
{
    #region Bold

    public static bool GetBold(DependencyObject obj)
    {
        return (bool)obj.GetValue(BoldProperty);
    }

    public static void SetBold(DependencyObject obj, bool value)
    {
        obj.SetValue(BoldProperty, value);
    }

    // Using a DependencyProperty as the backing store for Bold.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty BoldProperty =
        DependencyProperty.RegisterAttached("Bold", typeof(bool), typeof(HighlightableTextBlock), new PropertyMetadata(false, Refresh));

    #endregion

    #region Italic

    public static bool GetItalic(DependencyObject obj)
    {
        return (bool)obj.GetValue(ItalicProperty);
    }

    public static void SetItalic(DependencyObject obj, bool value)
    {
        obj.SetValue(ItalicProperty, value);
    }

    // Using a DependencyProperty as the backing store for Italic.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty ItalicProperty =
        DependencyProperty.RegisterAttached("Italic", typeof(bool), typeof(HighlightableTextBlock), new PropertyMetadata(false, Refresh));

    #endregion

    #region Underline

    public static bool GetUnderline(DependencyObject obj)
    {
        return (bool)obj.GetValue(UnderlineProperty);
    }

    public static void SetUnderline(DependencyObject obj, bool value)
    {
        obj.SetValue(UnderlineProperty, value);
    }

    // Using a DependencyProperty as the backing store for Underline.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty UnderlineProperty =
        DependencyProperty.RegisterAttached("Underline", typeof(bool), typeof(HighlightableTextBlock), new PropertyMetadata(false, Refresh));

    #endregion

    #region HighlightTextBrush

    public static Brush GetHighlightTextBrush(DependencyObject obj)
    {
        return (Brush)obj.GetValue(HighlightTextBrushProperty);
    }

    public static void SetHighlightTextBrush(DependencyObject obj, Brush value)
    {
        obj.SetValue(HighlightTextBrushProperty, value);
    }

    // Using a DependencyProperty as the backing store for HighlightTextBrush.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty HighlightTextBrushProperty =
        DependencyProperty.RegisterAttached("HighlightTextBrush", typeof(Brush), typeof(HighlightableTextBlock), new PropertyMetadata(SystemColors.HighlightTextBrush, Refresh));

    #endregion

    #region HighlightBrush

    public static Brush GetHighlightBrush(DependencyObject obj)
    {
        return (Brush)obj.GetValue(HighlightBrushProperty);
    }

    public static void SetHighlightBrush(DependencyObject obj, Brush value)
    {
        obj.SetValue(HighlightBrushProperty, value);
    }

    // Using a DependencyProperty as the backing store for HighlightBrush.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty HighlightBrushProperty =
        DependencyProperty.RegisterAttached("HighlightBrush", typeof(Brush), typeof(HighlightableTextBlock), new PropertyMetadata(SystemColors.HighlightBrush, Refresh));

    #endregion

    #region HighlightText

    public static string GetHightlightText(DependencyObject obj)
    {
        return (string)obj.GetValue(HightlightTextProperty);
    }

    public static void SetHightlightText(DependencyObject obj, string value)
    {
        obj.SetValue(HightlightTextProperty, value);
    }

    // Using a DependencyProperty as the backing store for HightlightText.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty HightlightTextProperty =
        DependencyProperty.RegisterAttached("HightlightText", typeof(string), typeof(HighlightableTextBlock), new PropertyMetadata(string.Empty, Refresh));

    #endregion

    #region InternalText

    protected static string GetInternalText(DependencyObject obj)
    {
        return (string)obj.GetValue(InternalTextProperty);
    }

    protected static void SetInternalText(DependencyObject obj, string value)
    {
        obj.SetValue(InternalTextProperty, value);
    }

    // Using a DependencyProperty as the backing store for InternalText.  This enables animation, styling, binding, etc...
    protected static readonly DependencyProperty InternalTextProperty =
        DependencyProperty.RegisterAttached("InternalText", typeof(string),
            typeof(HighlightableTextBlock), new PropertyMetadata(string.Empty, OnInternalTextChanged));

    private static void OnInternalTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var textblock = d as TextBlock;

        if (textblock != null)
        {
            textblock.Text = e.NewValue as string;
            Highlight(textblock);
        }
    }

    #endregion

    #region  IsBusy 

    private static bool GetIsBusy(DependencyObject obj)
    {
        return (bool)obj.GetValue(IsBusyProperty);
    }

    private static void SetIsBusy(DependencyObject obj, bool value)
    {
        obj.SetValue(IsBusyProperty, value);
    }

    // Using a DependencyProperty as the backing store for IsBusy.  This enables animation, styling, binding, etc...
    private static readonly DependencyProperty IsBusyProperty =
        DependencyProperty.RegisterAttached("IsBusy", typeof(bool), typeof(HighlightableTextBlock), new PropertyMetadata(false));

    #endregion

    #region Active

    public static bool GetActive(DependencyObject obj)
    {
        return (bool)obj.GetValue(ActiveProperty);
    }

    public static void SetActive(DependencyObject obj, bool value)
    {
        obj.SetValue(ActiveProperty, value);
    }

    public static readonly DependencyProperty ActiveProperty =
        DependencyProperty.RegisterAttached("Active", typeof(bool), typeof(HighlightableTextBlock), new PropertyMetadata(true, OnActiveChanged));

    private static void OnActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        TextBlock textBlock = d as TextBlock;
        bool isActive = (bool)e.NewValue;

        if (isActive)
            Highlight(textBlock);
        else
            ClearHighlight(textBlock);
    }

    #endregion

    #region Case Sensitive

    public static bool GetCaseSensitive(DependencyObject obj)
    {
        return (bool)obj.GetValue(CaseSensitiveProperty);
    }

    public static void SetCaseSensitive(DependencyObject obj, bool value)
    {
        obj.SetValue(CaseSensitiveProperty, value);
    }

    public static readonly DependencyProperty CaseSensitiveProperty =
        DependencyProperty.RegisterAttached("CaseSensitive", typeof(bool), typeof(HighlightableTextBlock), new PropertyMetadata(false, Refresh));

    #endregion

    #region Methods

    private static void Refresh(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        Highlight(d as TextBlock);
    }

    private static void Highlight(TextBlock textblock)
    {
        bool isActive = GetActive(textblock);

        if(!isActive)
            return;

        if (textblock == null) return;

        string text = textblock.Text;

        if (textblock.GetBindingExpression(HighlightableTextBlock.InternalTextProperty) == null)
        {
            var textBinding = textblock.GetBindingExpression(TextBlock.TextProperty);

            if (textBinding != null)
            {
                textblock.SetBinding(HighlightableTextBlock.InternalTextProperty, textBinding.ParentBindingBase);

                var propertyDescriptor = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));

                propertyDescriptor.RemoveValueChanged(textblock, OnTextChanged);
            }
            else
            {
                var propertyDescriptor = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));

                propertyDescriptor.AddValueChanged(textblock, OnTextChanged);

                textblock.Unloaded -= Textblock_Unloaded;
                textblock.Unloaded += Textblock_Unloaded;
            }
        }

        if (!string.IsNullOrEmpty(text))
        {
            SetIsBusy(textblock, true);

            var toHighlight = GetHightlightText(textblock);

            if (!string.IsNullOrEmpty(toHighlight))
            {
                RegexOptions regexOptions = RegexOptions.None;

                if (!GetCaseSensitive(textblock))
                    regexOptions ^= RegexOptions.IgnoreCase;

                var matches = Regex.Split(text, string.Format("({0})", Regex.Escape(toHighlight)), regexOptions);

                textblock.Inlines.Clear();

                var highlightBrush = GetHighlightBrush(textblock);
                var highlightTextBrush = GetHighlightTextBrush(textblock);

                foreach (var subString in matches)
                {
                    if (string.Compare(subString, toHighlight, true) == 0)
                    {
                        var formattedText = new Run(subString)
                        {
                            Background = highlightBrush,
                            Foreground = highlightTextBrush,
                        };

                        if (GetBold(textblock))
                        {
                            formattedText.FontWeight = FontWeights.Bold;
                        }

                        if (GetItalic(textblock))
                        {
                            formattedText.FontStyle = FontStyles.Italic;
                        }

                        if (GetUnderline(textblock))
                        {
                            formattedText.TextDecorations.Add(TextDecorations.Underline);
                        }

                        textblock.Inlines.Add(formattedText);
                    }
                    else
                    {
                        textblock.Inlines.Add(subString);
                    }
                }
            }
            else
            {
                textblock.Inlines.Clear();
                textblock.SetCurrentValue(TextBlock.TextProperty, text);
            }

            SetIsBusy(textblock, false);
        }
    }

    private static void ClearHighlight(TextBlock textBlock)
    {
        string text = textBlock.Text;


        textBlock.Inlines.Clear();
        textBlock.SetCurrentValue(TextBlock.TextProperty, text);
    }

    private static void Textblock_Unloaded(object sender, RoutedEventArgs e)
    {
        var propertyDescriptor = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));

        propertyDescriptor.RemoveValueChanged(sender as TextBlock, OnTextChanged);
    }

    private static void OnTextChanged(object sender, EventArgs e)
    {
        var textBlock = sender as TextBlock;

        if (textBlock != null &&
            !GetIsBusy(textBlock))
        {
            Highlight(textBlock);
        }
    }

    #endregion
}
