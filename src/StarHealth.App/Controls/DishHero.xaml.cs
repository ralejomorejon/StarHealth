using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace StarHealth.App.Controls;

/// <summary>
/// Interactive dish hero: drag to orbit with damped inertia (OrbitControls
/// style), slow idle drift like autoRotate, clamped elevation. All physics is
/// time-based (deg/sec, exponential decay) so motion is identical at any
/// frame rate; the loop runs at ~60fps to match display vsync. Drag marks
/// pointer events handled so the parent ScrollViewer never steals a stroke.
/// Honors Windows reduced-motion by freezing time.
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

    // OrbitControls-flavored tuning.
    private const double DragAz = 0.25;      // deg per px, horizontal
    private const double DragEl = 0.15;      // deg per px, vertical
    private const double Damping = 4.5;      // 1/s exponential velocity decay
    private const double DriftSpeed = 10.0;  // deg/s idle drift (~autoRotate)
    private const double MaxFling = 720.0;   // deg/s clamp
    private const double MinEl = 15.0;
    private const double MaxEl = 75.0;

    private static readonly InputCursor HandCursor =
        InputSystemCursor.Create(InputSystemCursorShape.Hand);
    private static readonly InputCursor MoveCursor =
        InputSystemCursor.Create(InputSystemCursorShape.SizeAll);

    private readonly DispatcherQueueTimer _timer;
    private readonly bool _reducedMotion;
    private double _camAz;
    private double _camEl = 55;
    private double _velAz; // deg/sec
    private double _velEl; // deg/sec
    private double _time;
    private bool _dragging;
    private double _lastX;
    private double _lastY;
    private DateTime _lastFrame = DateTime.UtcNow;
    private DateTime _lastMove = DateTime.UtcNow;
    private DateTime _lastTouch = DateTime.MinValue;
    private readonly DateTime _started = DateTime.UtcNow;

    public DishHero()
    {
        InitializeComponent();
        try
        {
            _reducedMotion = !new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;
        }
        catch { _reducedMotion = false; }
        ProtectedCursor = HandCursor;
        _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(16); // ~60fps, vsync-friendly
        _timer.Tick += (_, _) => Canvas.Invalidate();
        _timer.Start();
        Unloaded += (_, _) => _timer.Stop();
    }

    private void OnPaint(object? sender, SkiaSharp.Views.Windows.SKPaintSurfaceEventArgs e)
    {
        try
        {
            var now = DateTime.UtcNow;
            double dt = Math.Clamp((now - _lastFrame).TotalSeconds, 0, 0.1);
            _lastFrame = now;
            if (_reducedMotion)
            {
                _time = 0;
            }
            else
            {
                if (!_dragging)
                {
                    double decay = Math.Exp(-dt * Damping);
                    _velAz *= decay;
                    _velEl *= decay;
                    _camAz = (_camAz + _velAz * dt) % 360;
                    _camEl = Math.Clamp(_camEl + _velEl * dt, MinEl, MaxEl);
                    if ((now - _lastTouch).TotalSeconds > 3)
                        _camAz = (_camAz + DriftSpeed * dt) % 360;
                }
                _time = (now - _started).TotalSeconds;
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
        _lastMove = DateTime.UtcNow;
        _velAz = 0;
        _velEl = 0;
        _lastTouch = DateTime.UtcNow;
        Canvas.CapturePointer(e.Pointer);
        ProtectedCursor = MoveCursor;
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging) return;
        var pt = e.GetCurrentPoint(Canvas);
        var now = DateTime.UtcNow;
        double dt = Math.Max((now - _lastMove).TotalSeconds, 1e-3);
        double dx = pt.Position.X - _lastX;
        double dy = pt.Position.Y - _lastY;
        _lastX = pt.Position.X;
        _lastY = pt.Position.Y;
        _lastMove = now;
        _camAz = (_camAz + dx * DragAz) % 360;
        _camEl = Math.Clamp(_camEl - dy * DragEl, MinEl, MaxEl);
        // Low-pass the release velocity so flings don't depend on mouse poll rate.
        _velAz = Math.Clamp(0.65 * (dx * DragAz / dt) + 0.35 * _velAz, -MaxFling, MaxFling);
        _velEl = Math.Clamp(0.65 * (-dy * DragEl / dt) + 0.35 * _velEl, -MaxFling, MaxFling);
        _lastTouch = now;
        e.Handled = true;
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _dragging = false;
        _lastTouch = DateTime.UtcNow;
        ProtectedCursor = HandCursor;
        e.Handled = true;
    }

    private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
        => ProtectedCursor = HandCursor;

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _dragging = false;
        ProtectedCursor = null;
    }
}
