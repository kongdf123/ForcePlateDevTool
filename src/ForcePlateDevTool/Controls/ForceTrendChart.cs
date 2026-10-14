using System.Windows;
using System.Windows.Media;
using ForcePlateDevTool.Models;
using ForcePlateDevTool.Services;

namespace ForcePlateDevTool.Controls;

/// <summary>
/// A bounded real-time trend chart for one pedal.
/// AddSample and Clear should be called on the UI thread.
/// </summary>
public sealed class ForceTrendChart : FrameworkElement
{
    private static readonly TimeSpan VisibleWindow =
        TimeSpan.FromSeconds(10);

    private readonly ForceSampleBuffer _buffer = new();

    private readonly Typeface _typeface =
        new("Segoe UI");

    public ForceTrendChart()
    {
        ClipToBounds = true;
        MinHeight = 180;
    }

    public int SampleCount => _buffer.SampleCount;

    public void AddSample(ForceSample sample)
    {
        _buffer.Add(sample);
        InvalidateVisual();
    }

    public void Clear()
    {
        _buffer.Clear();
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        IReadOnlyList<ForceSample> samples = _buffer.Samples;

        double width = ActualWidth;
        double height = ActualHeight;

        dc.DrawRectangle(
            Brushes.White,
            null,
            new Rect(0, 0, width, height));

        if (width < 100 || height < 80)
            return;

        const double left = 42;
        const double right = 12;
        const double top = 12;
        const double bottom = 26;

        double plotWidth = width - left - right;
        double plotHeight = height - top - bottom;

        if (plotWidth <= 0 || plotHeight <= 0)
            return;

        var gridPen = new Pen(
            new SolidColorBrush(Color.FromRgb(225, 230, 237)),
            1);

        for (int i = 0; i <= 4; i++)
        {
            double y = top + plotHeight * i / 4.0;

            dc.DrawLine(
                gridPen,
                new Point(left, y),
                new Point(left + plotWidth, y));
        }

        if (samples.Count < 2)
        {
            DrawText(
                dc,
                "Waiting for decoded samples...",
                12,
                Brushes.Gray,
                new Point(left + 8, top + 8));

            return;
        }

        float min = samples
            .SelectMany(s => new[] { s.Fx, s.Fy, s.Fz })
            .Where(float.IsFinite)
            .DefaultIfEmpty(-1)
            .Min();

        float max = samples
            .SelectMany(s => new[] { s.Fx, s.Fy, s.Fz })
            .Where(float.IsFinite)
            .DefaultIfEmpty(1)
            .Max();

        // Include zero and avoid a zero-height scale.
        min = Math.Min(min, 0);
        max = Math.Max(max, 0);

        if (max - min < 0.001f)
        {
            min -= 1;
            max += 1;
        }

        double range = max - min;
        DateTime newest = samples[^1].Timestamp;
        DateTime oldest = newest - VisibleWindow;

        DrawText(
            dc,
            max.ToString("F1"),
            10,
            Brushes.Gray,
            new Point(2, top - 5));

        DrawText(
            dc,
            min.ToString("F1"),
            10,
            Brushes.Gray,
            new Point(2, top + plotHeight - 8));

        DrawText(
            dc,
            "10 s",
            10,
            Brushes.Gray,
            new Point(left, top + plotHeight + 5));

        DrawText(
            dc,
            "Now",
            10,
            Brushes.Gray,
            new Point(left + plotWidth - 24, top + plotHeight + 5));

        DrawSeries(
            dc, samples, s => s.Fx,
            Color.FromRgb(37, 99, 235),
            oldest, newest, min, range,
            left, top, plotWidth, plotHeight);

        DrawSeries(
            dc, samples, s => s.Fy,
            Color.FromRgb(217, 119, 6),
            oldest, newest, min, range,
            left, top, plotWidth, plotHeight);

        DrawSeries(
            dc, samples, s => s.Fz,
            Color.FromRgb(5, 150, 105),
            oldest, newest, min, range,
            left, top, plotWidth, plotHeight);
    }

    private static void DrawSeries(
        DrawingContext dc,
        IReadOnlyList<ForceSample> samples,
        Func<ForceSample, float> selector,
        Color color,
        DateTime oldest,
        DateTime newest,
        float min,
        double range,
        double left,
        double top,
        double plotWidth,
        double plotHeight)
    {
        var geometry = new StreamGeometry();

        using (var ctx = geometry.Open())
        {
            bool started = false;

            foreach (var sample in samples)
            {
                float value = selector(sample);

                if (!float.IsFinite(value))
                {
                    started = false;
                    continue;
                }

                double timeRange =
                    Math.Max(0.001, (newest - oldest).TotalSeconds);

                double x = left +
                    (sample.Timestamp - oldest).TotalSeconds /
                    timeRange * plotWidth;

                double y = top +
                    (1.0 - (value - min) / range) * plotHeight;

                var point = new Point(x, y);

                if (!started)
                {
                    ctx.BeginFigure(point, false, false);
                    started = true;
                }
                else
                {
                    ctx.LineTo(point, true, false);
                }
            }
        }

        geometry.Freeze();

        dc.DrawGeometry(
            null,
            new Pen(new SolidColorBrush(color), 1.5),
            geometry);
    }

    private void DrawText(
        DrawingContext dc,
        string text,
        double size,
        Brush brush,
        Point origin)
    {
        var formatted = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            _typeface,
            size,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        dc.DrawText(formatted, origin);
    }
}