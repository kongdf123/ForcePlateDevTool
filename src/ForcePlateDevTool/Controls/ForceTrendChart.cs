using System.Globalization;
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

        if (width < 140 || height < 100)
            return;

        const double left = 62;
        const double right = 16;
        const double top = 24;
        const double bottom = 48;

        double plotWidth = width - left - right;
        double plotHeight = height - top - bottom;

        if (plotWidth <= 0 || plotHeight <= 0)
            return;

        var gridPen = new Pen(
            new SolidColorBrush(Color.FromRgb(225, 230, 237)),
            1);

        var axisPen = new Pen(
            new SolidColorBrush(Color.FromRgb(100, 116, 139)),
            1.2);

        float[] values = samples
            .SelectMany(s => new[] { s.Fx, s.Fy, s.Fz })
            .Where(float.IsFinite)
            .ToArray();

        float min = values.Length > 0 ? values.Min() : -1f;
        float max = values.Length > 0 ? values.Max() : 1f;

        min = Math.Min(min, 0f);
        max = Math.Max(max, 0f);

        if (max - min < 0.001f)
        {
            min -= 1f;
            max += 1f;
        }

        float padding = (max - min) * 0.05f;
        min -= padding;
        max += padding;

        double range = max - min;

        for (int i = 0; i <= 4; i++)
        {
            double fraction = i / 4.0;
            double y = top + fraction * plotHeight;
            double forceValue = max - fraction * range;

            dc.DrawLine(
                gridPen,
                new Point(left, y),
                new Point(left + plotWidth, y));

            DrawText(
                dc,
                forceValue.ToString(
                    "G4",
                    CultureInfo.InvariantCulture),
                10,
                Brushes.Gray,
                new Point(2, y - 7));
        }

        for (int i = 0; i <= 4; i++)
        {
            double fraction = i / 4.0;
            double x = left + fraction * plotWidth;

            dc.DrawLine(
                gridPen,
                new Point(x, top),
                new Point(x, top + plotHeight));

            double msAgo =
                VisibleWindow.TotalMilliseconds * (1.0 - fraction);

            string label = msAgo == 0
                ? "0 ms"
                : $"-{msAgo:0} ms";

            DrawText(
                dc,
                label,
                10,
                Brushes.Gray,
                new Point(x - 18, top + plotHeight + 6));
        }

        dc.DrawLine(
            axisPen,
            new Point(left, top),
            new Point(left, top + plotHeight));

        dc.DrawLine(
            axisPen,
            new Point(left, top + plotHeight),
            new Point(left + plotWidth, top + plotHeight));

        DrawText(
            dc,
            "Force (N)",
            11,
            Brushes.DimGray,
            new Point(2, 2));

        DrawText(
            dc,
            "Time(ms)",
            11,
            Brushes.DimGray,
            new Point(
                left + plotWidth / 2 - 24,
                top + plotHeight + 27));

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

        DateTime newest = samples[^1].Timestamp;
        DateTime oldest = newest - VisibleWindow;

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