using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace StarHealth.App.Controls;

/// <summary>
/// Interactive dish hero: drag to orbit, inertia on release, slow idle drift.
/// Camera state lives here; all drawing lives in <see cref="DishHeroScene"/>
/// (UI-free, shared with the PNG harness). Honors Windows reduced-motion by
/// freezing time.
/// </summary>
public sealed partial class DishHero : UserControl
{
    public static readonly DependencyProperty TiltProperty =
        DependencyProperty.Register(nameof(Tilt), typeof(double),
            typeof(DishHero), new PropertyMetadata(15.0));

    public static readonly DependencyProperty BeamProperty =
        DependencyProperty.Register(nameof(Beam), typeof(int),
            typeof(DishHero), new PropertyMetadata(DishHeroScene.BeamOk));

    public double Tilt
    {
        get => (double)GetValue(TiltProperty);
        set => SetValue(TiltProperty, value);
    }

    /// <summary>0 ok, 1 warn, 2 bad/offline (see DishHeroScene).</summary>
    public int Beam
    {
        get => (int)GetValue(BeamProperty);
        set => SetValue(BeamProperty, value);
    }

    private readonly DispatcherQueueTimer _timer;
    private readonly bool _reducedMotion;
    private double _camAz;
    private double _camEl = 55;
    private double _velAz;
    private double _time;
    private bool _dragging;
    private double _lastX;
    private double _lastY;
    private DateTime _lastTouch = DateTime.MinValue;
    private DateTime _started = DateTime.UtcNow;

    public DishHero()
    {
        InitializeComponent();
        try
        {
            _reducedMotion = !new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;
        }
        catch { _reducedMotion = false; }
        _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(33);
        _timer.Tick += (_, _) => Canvas.Invalidate();
        _timer.Start();
        Unloaded += (_, _) => _timer.Stop();
    }

    private void OnPaint(object? sender, SkiaSharp.Views.Windows.SKPaintSurfaceEventArgs e)
    {
        try
        {
            if (_reducedMotion)
            {
                _time = 0;
            }
            else
            {
                if (!_dragging)
                {
                    _camAz += _velAz;
                    _velAz *= 0.94;
                    if ((DateTime.UtcNow - _lastTouch).TotalSeconds > 3)
                        _camAz += 0.35; // idle drift
                }
                _time = (DateTime.UtcNow - _started).TotalSeconds;
            }
            DishHeroScene.Draw(e.Surface.Canvas, e.Info.Width, e.Info.Height,
                Tilt, Beam, _camAz, _camEl, _time);
        }
        catch { /* a bad frame must never take the page down */ }
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var pt = e.GetCurrentPoint(Canvas);
        _dragging = true;
        _lastX = pt.Position.X;
        _lastY = pt.Position.Y;
        _velAz = 0;
        _lastTouch = DateTime.UtcNow;
        Canvas.CapturePointer(e.Pointer);
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging) return;
        var pt = e.GetCurrentPoint(Canvas);
        double dx = pt.Position.X - _lastX;
        double dy = pt.Position.Y - _lastY;
        _lastX = pt.Position.X;
        _lastY = pt.Position.Y;
        _camAz = (_camAz + dx * 0.25) % 360;
        _camEl = Math.Clamp(_camEl - dy * 0.15, 15, 75);
        _velAz = dx * 0.25;
        _lastTouch = DateTime.UtcNow;
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _dragging = false;
        _lastTouch = DateTime.UtcNow;
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e) => _dragging = false;
}
