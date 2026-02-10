using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Diffy.Core.Models;

namespace Diffy.App.Controls;

public class HighlightedTextBlock : TextBlock
{
    private static readonly ConcurrentDictionary<string, SolidColorBrush> _brushPool = new();
    public static readonly StyledProperty<string?> SourceTextProperty =
        AvaloniaProperty.Register<HighlightedTextBlock, string?>(nameof(SourceText));

    public string? SourceText
    {
        get => GetValue(SourceTextProperty);
        set => SetValue(SourceTextProperty, value);
    }

    public static readonly StyledProperty<List<HighlightedSegment>?> HighlightsProperty =
        AvaloniaProperty.Register<HighlightedTextBlock, List<HighlightedSegment>?>(nameof(Highlights));

    public List<HighlightedSegment>? Highlights
    {
        get => GetValue(HighlightsProperty);
        set => SetValue(HighlightsProperty, value);
    }

    static HighlightedTextBlock()
    {
        HighlightsProperty.Changed.AddClassHandler<HighlightedTextBlock>((x, e) => x.OnHighlightsChanged(e));
        SourceTextProperty.Changed.AddClassHandler<HighlightedTextBlock>((x, e) => x.OnHighlightsChanged(e));
    }

    private void OnHighlightsChanged(AvaloniaPropertyChangedEventArgs e)
    {
        UpdateInlines();
    }

    private void UpdateInlines()
    {
        var text = SourceText;
        var highlights = Highlights;

        // Ensure we don't have base Text property conflicting
        if (Text != null) Text = null;

        Inlines?.Clear();

        if (string.IsNullOrEmpty(text))
            return;

        if (highlights == null || highlights.Count == 0)
        {
            Inlines?.Add(new Run(text));
            return;
        }

        int lastIndex = 0;
        foreach (var segment in highlights)
        {
            // Add plain text before match
            if (segment.Offset > lastIndex)
            {
                var plainText = text.Substring(lastIndex, segment.Offset - lastIndex);
                Inlines?.Add(new Run(plainText));
            }

            // Add highlighted segment
            var segmentLength = Math.Min(segment.Length, text.Length - segment.Offset);
            if (segmentLength > 0)
            {
                var highlightedText = text.Substring(segment.Offset, segmentLength);
                var run = new Run(highlightedText);

                if (!string.IsNullOrEmpty(segment.ColorHex))
                {
                    if (Color.TryParse(segment.ColorHex, out var color))
                    {
                        run.Foreground = _brushPool.GetOrAdd(segment.ColorHex, hex => new SolidColorBrush(color));
                    }
                }

                if (!string.IsNullOrEmpty(segment.BackgroundHex))
                {
                    if (Color.TryParse(segment.BackgroundHex, out var color))
                    {
                        run.Background = _brushPool.GetOrAdd(segment.BackgroundHex, hex => new SolidColorBrush(color));
                    }
                }

                if (segment.IsBold) run.FontWeight = FontWeight.Bold;
                if (segment.IsItalic) run.FontStyle = FontStyle.Italic;

                Inlines?.Add(run);
                lastIndex = segment.Offset + segmentLength;
            }
        }

        // Add remaining text
        if (lastIndex < text.Length)
        {
            Inlines?.Add(new Run(text.Substring(lastIndex)));
        }
    }
}
