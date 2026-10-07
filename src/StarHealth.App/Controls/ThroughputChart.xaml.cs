using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using StarHealth.Core.Models;
using Windows.Foundation;

namespace StarHealth.App.Controls;

/// <summary>
/// Dependency-free line chart (Canvas + Polyline only) so the exact same
/// control compiles under Uno on every target. Downsamples to pixel width.
/// </summary>
public sealed partial class ThroughputChart : UserControl
{
    public static readonly DependencyProperty SamplesProperty =
        DependencyProperty.Register(nameof(Samples), typeof(IReadOnlyList<ThroughputSample>),
            typeof(ThroughputChart), new PropertyMetadata(null, OnDataChanged));

    public static readonly DependencyProperty MaxProperty =
        DependencyProperty.Register(nameof(Max), typeof(double),
            typeof(ThroughputChart), new PropertyMetadata(100.0, OnDataChanged));

    public IReadOnlyList<ThroughputSample>? Samples
    {
        get => (IReadOnlyList<ThroughputSample>?)GetValue(SamplesProperty);
        set => SetValue(SamplesProperty, value);
    }

    public double Max
    {
        get => (double)GetValue(MaxProperty);
        set => SetValue(MaxProperty, value);
    }

    public ThroughputChart() => InitializeComponent();

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ThroughputChart)d).Redraw();

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Redraw();

    private void OnPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        HoverLine.Visibility = Visibility.Collapsed;
        HoverCard.Visibility = Visibility.Collapsed;
    }

    private void OnPointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var samples = Samples;
        double w = ChartCanvas.ActualWidth, h = ChartCanvas.ActualHeight;
        if (samples is null || samples.Count == 0 || w <= 0 || h <= 0)
        {
            OnPointerExited(sender, e);
            return;
        }
        var pt = e.GetCurrentPoint(ChartCanvas).Position;
        int idx = Math.Clamp((int)Math.Round(pt.X / w * (samples.Count - 1)), 0, samples.Count - 1);
        var s = samples[idx];
        double x = samples.Count <= 1 ? 0 : (double)idx / (samples.Count - 1) * w;

        HoverLine.X1 = HoverLine.X2 = x;
        HoverLine.Y1 = 0;
        HoverLine.Y2 = h - BottomPad;
        HoverLine.Visibility = Visibility.Visible;

        HoverTime.Text = s.Timestamp.LocalDateTime.ToString("HH:mm:ss");
        HoverDown.Text = $"↓ {s.DownMbps:F1} Mbps";
        HoverUp.Text = $"↑ {s.UpMbps:F1} Mbps";
        // Language-neutral: arrows + numbers need no translation.
        const double cardW = 150;
        Canvas.SetLeft(HoverCard, x + 12 + cardW > w ? x - 12 - cardW : x + 12);
        Canvas.SetTop(HoverCard, Math.Clamp(pt.Y - 20, 0, Math.Max(0, h - 80)));
        HoverCard.Visibility = Visibility.Visible;
    }

    private const double TopPad = 4;
    private const double BottomPad = 24; // legend row lives here, clear of the lines

    private void Redraw()
    {
        var samples = Samples;
        double w = ChartCanvas.ActualWidth, h = ChartCanvas.ActualHeight;
        if (samples is null || samples.Count < 2 || w <= 0 || h <= 0) return;
        double max = Math.Max(1, Max);
        MaxLabel.Text = $"{max:F0} Mbps";

        double plotH = h - TopPad - BottomPad;
        foreach (var (line, y) in new[] { (Grid1, 0.25), (Grid2, 0.5), (Grid3, 0.75) })
        {
            line.X1 = 0; line.X2 = w;
            line.Y1 = line.Y2 = TopPad + plotH * y;
        }

        int stride = Math.Max(1, (int)(samples.Count / Math.Max(1, w / 2)));
        DownLine.Points = ToPoints(samples, stride, w, h, max, static p => p.DownMbps);
        UpLine.Points = ToPoints(samples, stride, w, h, max, static p => p.UpMbps);
    }

    private static PointCollection ToPoints(IReadOnlyList<ThroughputSample> samples, int stride,
        double w, double h, double max, Func<ThroughputSample, double> pick)
    {
        var pts = new PointCollection();
        int n = (samples.Count + stride - 1) / stride;
        for (int i = 0; i < n; i++)
        {
            var s = samples[Math.Min(samples.Count - 1, i * stride)];
            double x = n <= 1 ? 0 : (double)i / (n - 1) * w;
            double y = TopPad + (1 - Math.Clamp(pick(s) / max, 0, 1)) * (h - TopPad - BottomPad);
            pts.Add(new Point(x, y));
        }
        return pts;
    }
}

