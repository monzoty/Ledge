using System.Windows;
using System.Windows.Media;

namespace Ledge.Views;

/// <summary>
/// A small ring that shows a fraction (DESIGN.md §2 "Pill timer"): a faint
/// full circle as the track, and an arc on top starting at 12 o'clock and
/// running clockwise for <see cref="Value"/> × 360°.
/// </summary>
/// <remarks>
/// WPF has no built-in ring, so this element draws itself in OnRender,
/// WPF's low-level drawing API (like drawing on a canvas). Each property is a
/// dependency property registered with AffectsRender, which tells WPF to call
/// OnRender again whenever it changes.
/// https://learn.microsoft.com/dotnet/api/system.windows.uielement.onrender
/// https://learn.microsoft.com/dotnet/desktop/wpf/graphics-multimedia/geometry-overview
/// </remarks>
public sealed class ProgressRing : FrameworkElement
{
    private const double FullCircle = 360;
    private const double QuarterTurn = 90;

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(ProgressRing),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness), typeof(double), typeof(ProgressRing),
        new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush), typeof(Brush), typeof(ProgressRing),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackOpacityProperty = DependencyProperty.Register(
        nameof(TrackOpacity), typeof(double), typeof(ProgressRing),
        new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ArcBrushProperty = DependencyProperty.Register(
        nameof(ArcBrush), typeof(Brush), typeof(ProgressRing),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>0 = no arc, 1 = full circle.</summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public Brush? TrackBrush
    {
        get => (Brush?)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public double TrackOpacity
    {
        get => (double)GetValue(TrackOpacityProperty);
        set => SetValue(TrackOpacityProperty, value);
    }

    public Brush? ArcBrush
    {
        get => (Brush?)GetValue(ArcBrushProperty);
        set => SetValue(ArcBrushProperty, value);
    }

    protected override void OnRender(DrawingContext drawing)
    {
        // The stroke is centred on the line, so pull the radius in by half
        // of it to keep the whole ring inside our bounds.
        var radius = (Math.Min(ActualWidth, ActualHeight) - StrokeThickness) / 2;
        if (radius <= 0)
        {
            return;
        }

        var center = new Point(ActualWidth / 2, ActualHeight / 2);

        // Track: the faint full circle.
        if (TrackBrush is not null)
        {
            drawing.PushOpacity(TrackOpacity);
            drawing.DrawEllipse(null, new Pen(TrackBrush, StrokeThickness), center, radius, radius);
            drawing.Pop();
        }

        if (ArcBrush is null)
        {
            return;
        }

        var fraction = Math.Clamp(Value, 0, 1);
        var pen = new Pen(ArcBrush, StrokeThickness);

        // A full circle can't be drawn as one arc (start and end are the same point).
        if (fraction >= 1)
        {
            drawing.DrawEllipse(null, pen, center, radius, radius);
            return;
        }

        if (fraction <= 0)
        {
            return;
        }

        var sweep = fraction * FullCircle;
        var start = PointOnCircle(center, radius, 0);
        var end = PointOnCircle(center, radius, sweep);

        var arc = new StreamGeometry();
        using (var context = arc.Open())
        {
            context.BeginFigure(start, isFilled: false, isClosed: false);
            context.ArcTo(
                end,
                new Size(radius, radius),
                rotationAngle: 0,
                isLargeArc: sweep > FullCircle / 2,
                SweepDirection.Clockwise,
                isStroked: true,
                isSmoothJoin: false);
        }

        arc.Freeze();
        drawing.DrawGeometry(null, pen, arc);
    }

    /// <summary>
    /// The point <paramref name="degrees"/> clockwise from 12 o'clock. Screen y
    /// grows downwards, so "12 o'clock" is −90° in the usual maths convention.
    /// </summary>
    private static Point PointOnCircle(Point center, double radius, double degrees)
    {
        var radians = (degrees - QuarterTurn) * Math.PI / (FullCircle / 2);
        return new Point(center.X + (radius * Math.Cos(radians)), center.Y + (radius * Math.Sin(radians)));
    }
}
