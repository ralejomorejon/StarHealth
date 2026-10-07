using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace StarHealth.App.Controls;

/// <summary>Single-series sparkline with hover readout. Same Canvas+Polyline
/// recipe as ThroughputChart: identical XAML runs on Uno targets.</summary>
public sealed partial class Sparkline : UserControl
{
    public static readonly DependencyProperty ValuesProperty =
        DependencyProperty.Register(nameof(Values), typeof(IList<double>),
            typeof(Sparkline), new PropertyMetadata(null, OnDataChanged));

    public static readonly DependencyProperty MaxProperty =
        DependencyProperty.Register(nameof(Max), typeof(double),
            typeof(Sparkline), new PropertyMetadata(0.0, OnDataChanged));

    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(nameof(Unit), typeof(string),
            typeof(Sparkline), new PropertyMetadata("", OnDataChanged));

    public IList<double>? Values
    {
        get => (IList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    /// <summary>Fixed ceiling; 0 = autoscale to the data.</summary>
    public double Max
    {
        get => (double)GetValue(MaxProperty);
        set => SetValue(MaxProperty, value);
    }

    /// <summary>Unit suffix shown in the hover readout (e.g. "%", "ms").</summary>
    public string Unit
    {
        get => (string)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public Sparkline() => InitializeComponent();

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((Sparkline)d).Redraw();

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Redraw();

    private void OnPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        HoverLine.Visibility = Visibility.Collapsed;
        HoverCard.Visibility = Visibility.Collapsed;
    }

    private void OnPointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var values = Values;
        double w = SparCanvas.ActualWidth, h = SparCanvas.ActualHeight;
        if (values is null || values.Count == 0 || w <= 0 || h <= 0)
        {
            OnPointerExited(sender, e);
            return;
        }
        var pt = e.GetCurrentPoint(SparCanvas).Position;
        int idx = Math.Clamp((int)Math.Round(pt.X / w * (values.Count - 1)), 0, values.Count - 1);
        double x = values.Count <= 1 ? 0 : (double)idx / (values.Count - 1) * w;

        HoverLine.X1 = HoverLine.X2 = x;
        HoverLine.Y1 = 0;
        HoverLine.Y2 = h;
        HoverLine.Visibility = Visibility.Visible;

        HoverValue.Text = $"{values[idx]:F1} {Unit}".Trim();
        const double cardW = 90;
        Canvas.SetLeft(HoverCard, x + 10 + cardW > w ? x - 10 - cardW : x + 10);
        Canvas.SetTop(HoverCard, Math.Clamp(pt.Y - 16, 0, Math.Max(0, h - 34)));
        HoverCard.Visibility = Visibility.Visible;
    }

    private void Redraw()
    {
        var values = Values;
        double w = SparCanvas.ActualWidth, h = SparCanvas.ActualHeight;
        if (values is null || values.Count < 2 || w <= 0 || h <= 0) return;
        double max = Max > 0 ? Max : Math.Max(1e-6, values.Max());

        int stride = Math.Max(1, (int)(values.Count / Math.Max(1, w / 2)));
        int n = (values.Count + stride - 1) / stride;
        var pts = new PointCollection();
        for (int i = 0; i < n; i++)
        {
            double v = values[Math.Min(values.Count - 1, i * stride)];
            double x = n <= 1 ? 0 : (double)i / (n - 1) * w;
            double y = h - Math.Clamp(v / max, 0, 1) * (h - 4) - 2;
            pts.Add(new Point(x, y));
        }
        Line.Points = pts;
    }
}
