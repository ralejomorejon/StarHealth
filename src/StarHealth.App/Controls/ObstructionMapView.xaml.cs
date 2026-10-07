using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace StarHealth.App.Controls;

/// <summary>
/// SNR heatmap (dish_get_obstruction_map). Downsamples large grids so the
/// element count stays small; same XAML/C# runs on Uno targets.
/// Colors: low signal = warm/red (likely blocked), high = cool/green.
/// </summary>
public sealed partial class ObstructionMapView : UserControl
{
    public static readonly DependencyProperty RowsProperty =
        DependencyProperty.Register(nameof(Rows), typeof(int),
            typeof(ObstructionMapView), new PropertyMetadata(0, OnDataChanged));
    public static readonly DependencyProperty ColsProperty =
        DependencyProperty.Register(nameof(Cols), typeof(int),
            typeof(ObstructionMapView), new PropertyMetadata(0, OnDataChanged));
    public static readonly DependencyProperty ValuesProperty =
        DependencyProperty.Register(nameof(Values), typeof(IList<float>),
            typeof(ObstructionMapView), new PropertyMetadata(null, OnDataChanged));

    public int Rows
    {
        get => (int)GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    public int Cols
    {
        get => (int)GetValue(ColsProperty);
        set => SetValue(ColsProperty, value);
    }

    public IList<float>? Values
    {
        get => (IList<float>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public ObstructionMapView() => InitializeComponent();

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ObstructionMapView)d).Redraw();

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Redraw();

    private void Redraw()
    {
        MapCanvas.Children.Clear();
        var values = Values;
        if (values is null || Rows <= 0 || Cols <= 0 || values.Count < Rows * Cols) return;
        double w = MapCanvas.ActualWidth, h = MapCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        // Cap rendered cells (~48 per side) by striding; dish sends up to 123x123.
        int stride = Math.Max(1, (int)Math.Ceiling(Math.Max(Rows, Cols) / 48.0));
        int r2 = (Rows + stride - 1) / stride, c2 = (Cols + stride - 1) / stride;
        float max = 0.01f;
        foreach (var v in values) if (v > max) max = v;

        double cw = w / c2, ch = h / r2;
        for (int r = 0; r < r2; r++)
            for (int c = 0; c < c2; c++)
            {
                float v = values[Math.Min(values.Count - 1, (r * stride) * Cols + c * stride)];
                var rect = new Rectangle
                {
                    Width = Math.Max(1, cw - 1),
                    Height = Math.Max(1, ch - 1),
                    Fill = new SolidColorBrush(ColorFor(v / max)),
                };
                Canvas.SetLeft(rect, c * cw);
                Canvas.SetTop(rect, r * ch);
                MapCanvas.Children.Add(rect);
            }
    }

    private static Windows.UI.Color ColorFor(float t) => t switch
    {
        < 0.25f => Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xE5, 0x6B, 0x6B),
        < 0.55f => Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xE8, 0xB4, 0x4F),
        _ => Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x6F, 0xD5, 0x98),
    };
}
