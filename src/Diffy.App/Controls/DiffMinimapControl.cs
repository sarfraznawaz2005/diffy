using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Diffy.Core.Models;

namespace Diffy.App.Controls;

/// <summary>
/// A minimap control that displays a bird's eye view of diff changes
/// with colored markers for additions (green) and deletions (red).
/// </summary>
public class DiffMinimapControl : Control
{
    #region Styled Properties

    public static readonly StyledProperty<IEnumerable<DiffLine>?> DiffLinesProperty =
        AvaloniaProperty.Register<DiffMinimapControl, IEnumerable<DiffLine>?>(nameof(DiffLines));

    public static readonly StyledProperty<ScrollViewer?> ScrollViewerProperty =
        AvaloniaProperty.Register<DiffMinimapControl, ScrollViewer?>(nameof(ScrollViewer));

    public static readonly StyledProperty<double> MinimapWidthProperty =
        AvaloniaProperty.Register<DiffMinimapControl, double>(nameof(MinimapWidth), 40.0);

    public static readonly StyledProperty<IBrush?> AddedColorProperty =
        AvaloniaProperty.Register<DiffMinimapControl, IBrush?>(nameof(AddedColor),
            new SolidColorBrush(Color.FromArgb(200, 76, 175, 80))); // Brighter green

    public static readonly StyledProperty<IBrush?> RemovedColorProperty =
        AvaloniaProperty.Register<DiffMinimapControl, IBrush?>(nameof(RemovedColor),
            new SolidColorBrush(Color.FromArgb(200, 244, 67, 54))); // Brighter red

    public static readonly StyledProperty<IBrush?> ViewportColorProperty =
        AvaloniaProperty.Register<DiffMinimapControl, IBrush?>(nameof(ViewportColor),
            new SolidColorBrush(Color.FromArgb(120, 80, 80, 80)));

    public static readonly StyledProperty<IBrush?> MinimapBackgroundProperty =
        AvaloniaProperty.Register<DiffMinimapControl, IBrush?>(nameof(MinimapBackground),
            new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)));

    public static readonly StyledProperty<IBrush?> BorderBrushProperty =
        AvaloniaProperty.Register<DiffMinimapControl, IBrush?>(nameof(BorderBrush),
            new SolidColorBrush(Color.FromArgb(80, 128, 128, 128)));

    public IEnumerable<DiffLine>? DiffLines
    {
        get => GetValue(DiffLinesProperty);
        set => SetValue(DiffLinesProperty, value);
    }

    public ScrollViewer? ScrollViewer
    {
        get => GetValue(ScrollViewerProperty);
        set => SetValue(ScrollViewerProperty, value);
    }

    public double MinimapWidth
    {
        get => GetValue(MinimapWidthProperty);
        set => SetValue(MinimapWidthProperty, value);
    }

    public IBrush? AddedColor
    {
        get => GetValue(AddedColorProperty);
        set => SetValue(AddedColorProperty, value);
    }

    public IBrush? RemovedColor
    {
        get => GetValue(RemovedColorProperty);
        set => SetValue(RemovedColorProperty, value);
    }

    public IBrush? ViewportColor
    {
        get => GetValue(ViewportColorProperty);
        set => SetValue(ViewportColorProperty, value);
    }

    public IBrush? MinimapBackground
    {
        get => GetValue(MinimapBackgroundProperty);
        set => SetValue(MinimapBackgroundProperty, value);
    }

    public IBrush? BorderBrush
    {
        get => GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    #endregion

    private DispatcherTimer? _scrollDebounceTimer;
    private bool _isPointerDragging;
    private IReadOnlyList<DiffLine>? _cachedLines;
    private IEnumerable<DiffLine>? _cachedLinesSource;

    static DiffMinimapControl()
    {
        AffectsRender<DiffMinimapControl>(
            DiffLinesProperty,
            ScrollViewerProperty,
            MinimapWidthProperty,
            AddedColorProperty,
            RemovedColorProperty,
            ViewportColorProperty,
            MinimapBackgroundProperty,
            BorderBrushProperty);

        AffectsMeasure<DiffMinimapControl>(MinimapWidthProperty);
    }

    public DiffMinimapControl()
    {
        ClipToBounds = true;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        Cursor = new Cursor(StandardCursorType.Hand);

        if (ScrollViewer != null)
        {
            ScrollViewer.ScrollChanged += OnScrollChanged;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (ScrollViewer != null)
        {
            ScrollViewer.ScrollChanged -= OnScrollChanged;
        }

        _scrollDebounceTimer?.Stop();
        _scrollDebounceTimer = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ScrollViewerProperty)
        {
            if (change.OldValue is ScrollViewer oldScrollViewer)
            {
                oldScrollViewer.ScrollChanged -= OnScrollChanged;
            }

            if (change.NewValue is ScrollViewer newScrollViewer)
            {
                newScrollViewer.ScrollChanged += OnScrollChanged;
            }
        }
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        // Immediate rendering during active pointer interaction (dragging minimap)
        if (_isPointerDragging)
        {
            InvalidateVisual();
            return;
        }

        // Debounce for passive scrolling - reuse single timer to avoid GC pressure
        if (_scrollDebounceTimer == null)
        {
            _scrollDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // 1 frame at 60fps for passive scroll
            };
            _scrollDebounceTimer.Tick += (s, args) =>
            {
                InvalidateVisual();
                _scrollDebounceTimer?.Stop();
            };
        }
        else
        {
            _scrollDebounceTimer.Stop();
        }

        _scrollDebounceTimer.Start();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(MinimapWidth, availableSize.Height);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        // Draw background with subtle shadow effect
        if (MinimapBackground != null)
        {
            context.DrawRectangle(MinimapBackground, null, new Rect(0, 0, bounds.Width, bounds.Height));
        }

        // Draw left border for depth
        if (BorderBrush != null)
        {
            context.DrawLine(new Pen(BorderBrush, 1), new Point(0, 0), new Point(0, bounds.Height));
        }

        // Cache the materialized list to avoid allocating on every render frame
        var source = DiffLines;
        if (source == null) return;
        if (!ReferenceEquals(source, _cachedLinesSource))
        {
            _cachedLinesSource = source;
            _cachedLines = source as IReadOnlyList<DiffLine> ?? source.ToList();
        }
        var lines = _cachedLines!;
        if (lines.Count == 0) return;

        // Calculate scale factor
        var totalLines = lines.Count;
        var heightPerLine = bounds.Height / totalLines;

        // Draw change markers
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            IBrush? brush = null;

            switch (line.Kind)
            {
                case DiffLineKind.Added:
                    brush = AddedColor;
                    break;
                case DiffLineKind.Removed:
                    brush = RemovedColor;
                    break;
            }

            if (brush != null)
            {
                var y = i * heightPerLine;
                var markerHeight = Math.Max(2, heightPerLine); // Minimum 2px height for visibility
                var rect = new Rect(0, y, bounds.Width, markerHeight);
                context.DrawRectangle(brush, null, rect);
            }
        }

        // Draw viewport indicator
        if (ScrollViewer != null)
        {
            var viewport = ScrollViewer.Viewport;
            var extent = ScrollViewer.Extent;
            var offset = ScrollViewer.Offset;

            if (extent.Height > 0)
            {
                var viewportRatio = viewport.Height / extent.Height;
                var offsetRatio = offset.Y / extent.Height;

                var viewportHeight = Math.Max(20, bounds.Height * viewportRatio);
                var viewportY = bounds.Height * offsetRatio;

                var viewportRect = new Rect(0, viewportY, bounds.Width, viewportHeight);

                // Draw viewport indicator with dark border on all sides
                if (ViewportColor != null)
                {
                    var borderBrush = new SolidColorBrush(Color.FromArgb(180, 60, 60, 60));
                    context.DrawRectangle(ViewportColor, new Pen(borderBrush, 1), viewportRect);
                }
            }
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var pointerPoint = e.GetCurrentPoint(this);
        if (pointerPoint.Properties.IsLeftButtonPressed)
        {
            _isPointerDragging = true;
        }

        HandlePointerInteraction(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            HandlePointerInteraction(e);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _isPointerDragging = false;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _isPointerDragging = false;
    }

    private void HandlePointerInteraction(PointerEventArgs e)
    {
        if (ScrollViewer == null || Bounds.Height <= 0)
            return;

        var position = e.GetPosition(this);
        var ratio = position.Y / Bounds.Height;

        var extent = ScrollViewer.Extent;
        var viewport = ScrollViewer.Viewport;

        // Calculate target offset, centering the viewport on the clicked position
        var targetOffset = (ratio * extent.Height) - (viewport.Height / 2);
        targetOffset = Math.Max(0, Math.Min(targetOffset, extent.Height - viewport.Height));

        ScrollViewer.Offset = new Vector(ScrollViewer.Offset.X, targetOffset);

        // Force immediate visual update during pointer interaction
        InvalidateVisual();
    }
}
