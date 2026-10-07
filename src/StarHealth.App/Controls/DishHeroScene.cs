using SkiaSharp;

namespace StarHealth.App.Controls;

/// <summary>
/// Pure SkiaSharp scene: floating dish slab over a starfield with a status
/// beam. Zero WinUI types, so the exact same drawing code renders here and
/// in the headless PNG harness. Dish geometry is procedural (no SpaceX art).
/// Angles in degrees. Camera orbits the slab; the slab carries the live tilt.
/// </summary>
public static class DishHeroScene
{
    public const int BeamOk = 0;
    public const int BeamWarn = 1;
    public const int BeamBad = 2;

    public static void Draw(SKCanvas canvas, int w, int h,
        double tiltDeg, int beam, double camAzDeg, double camElDeg, double timeSec)
    {
        canvas.Clear(SKColors.Transparent);

        float cx = w * 0.5f;
        float cy = h * 0.68f;
        float scale = Math.Min(w, h) * 0.24f;

        DrawStars(canvas, w, h, camAzDeg, timeSec);
        var anchor = DishAnchor(cx, cy, scale, tiltDeg, camAzDeg, camElDeg);
        var tip = BeamTip(cx, cy, scale, tiltDeg, camAzDeg, camElDeg);
        DrawBeamHalo(canvas, anchor, tip, scale, beam, timeSec);
        DrawDish(canvas, cx, cy, scale, tiltDeg, camAzDeg, camElDeg, beam);
        DrawBeamCore(canvas, anchor, tip, scale, timeSec);
        DrawSatellite(canvas, anchor, tip, scale, timeSec);
    }

    // ---- stars ---------------------------------------------------------

    private static void DrawStars(SKCanvas canvas, int w, int h, double camAzDeg, double timeSec)
    {
        var rng = new Random(20261007);
        // (count, size, parallax, twinkleSpeed)
        var layers = new[] { (110, 1.1f, 0.02f, 0.7), (60, 1.7f, 0.05f, 1.3), (28, 2.4f, 0.10f, 2.1) };
        using var paint = new SKPaint { IsAntialias = true };
        double drift = camAzDeg % 360;
        for (int l = 0; l < layers.Length; l++)
        {
            var (count, size, par, speed) = layers[l];
            for (int i = 0; i < count; i++)
            {
                double x = rng.NextDouble() * w;
                double y = rng.NextDouble() * h;
                double phase = rng.NextDouble() * Math.PI * 2;
                double px = Mod(x - drift * par * w / 360.0, w);
                float tw = (float)(0.45 + 0.55 * (0.5 + 0.5 * Math.Sin(timeSec * speed + phase)));
                byte a = (byte)(140 * tw + 40);
                paint.Color = new SKColor(200, 214, 235, a);
                canvas.DrawCircle((float)px, (float)y, size, paint);
            }
        }
    }

    private static double Mod(double v, double m) => ((v % m) + m) % m;

    // ---- projection -----------------------------------------------------

    private readonly record struct V3(double X, double Y, double Z);

    private static V3 RotX(V3 v, double deg)
    {
        double r = deg * Math.PI / 180, c = Math.Cos(r), s = Math.Sin(r);
        return new V3(v.X, v.Y * c - v.Z * s, v.Y * s + v.Z * c);
    }

    private static V3 RotZ(V3 v, double deg)
    {
        double r = deg * Math.PI / 180, c = Math.Cos(r), s = Math.Sin(r);
        return new V3(v.X * c - v.Y * s, v.X * s + v.Y * c, v.Z);
    }

    private static SKPoint Project(V3 v, float cx, float cy, float scale)
        => new(cx + (float)(v.X * scale), cy - (float)(v.Z * scale));

    /// <summary>
    /// Dish-local slab point to camera space. Chain: dish tilt about X,
    /// camera orbit about Z, camera elevation (0 = horizon, 90 = top-down).
    /// </summary>
    private static V3 DishPoint(double x, double y, double z,
        double tiltDeg, double camAzDeg, double camElDeg)
    {
        var v = RotX(new V3(x, y, z), tiltDeg);
        v = RotZ(v, camAzDeg);
        v = RotX(v, -camElDeg);
        return v;
    }

    /// <summary>World-space face normal after dish tilt + camera orbit.</summary>
    private static V3 FaceNormal(double tiltDeg, double camAzDeg)
        => RotZ(RotX(new V3(0, 0, 1), tiltDeg), camAzDeg);

    /// <summary>World point just above the slab center (beam origin).</summary>
    private static SKPoint DishAnchor(float cx, float cy, float scale,
        double tiltDeg, double camAzDeg, double camElDeg)
    {
        var top = DishPoint(0, 0, 0.10, tiltDeg, camAzDeg, camElDeg);
        return Project(top, cx, cy, scale);
    }

    /// <summary>Beam tip: face center pushed along the face normal, in screen space.</summary>
    private static SKPoint BeamTip(float cx, float cy, float scale,
        double tiltDeg, double camAzDeg, double camElDeg)
    {
        const double beamLen = 2.0;
        var n = FaceNormal(tiltDeg, camAzDeg);
        var v = RotX(new V3(n.X * beamLen, n.Y * beamLen, 0.10 + n.Z * beamLen), -camElDeg);
        return Project(v, cx, cy, scale);
    }

    // ---- dish ------------------------------------------------------------

    private static void DrawDish(SKCanvas canvas, float cx, float cy, float scale,
        double tiltDeg, double camAzDeg, double camElDeg, int beam)
    {
        const double hw = 1.35, hd = 0.95, ht = 0.07; // Mini-ish slab proportions
        V3[] top =
        {
            DishPoint(-hw, -hd, ht, tiltDeg, camAzDeg, camElDeg),
            DishPoint(hw, -hd, ht, tiltDeg, camAzDeg, camElDeg),
            DishPoint(hw, hd, ht, tiltDeg, camAzDeg, camElDeg),
            DishPoint(-hw, hd, ht, tiltDeg, camAzDeg, camElDeg),
        };
        // Bottom ring: same x/y footprint at z=0 through the same transform chain.
        var raw = new (double X, double Y)[]
        {
            (-hw, -hd), (hw, -hd), (hw, hd), (-hw, hd),
        };
        V3[] bot = raw.Select(p => DishPoint(p.X, p.Y, 0, tiltDeg, camAzDeg, camElDeg)).ToArray();

        // Offline/dimmed dish when the link is down (P1 live binding).
        float dim = beam == BeamBad ? 0.55f : 1.0f;
        using var topPaint = new SKPaint { IsAntialias = true, Color = Dim(new SKColor(226, 232, 240), dim) };
        using var sidePaint = new SKPaint { IsAntialias = true, Color = Dim(new SKColor(148, 158, 175), dim) };
        using var darkPaint = new SKPaint { IsAntialias = true, Color = Dim(new SKColor(96, 105, 122), dim) };
        using var edgePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Math.Max(1, scale * 0.006f),
            Color = new SKColor(255, 255, 255, 60),
        };

        var path = new SKPath();
        // Two visible side skirts (pick by camera height: draw far edges darker).
        void Quad(V3 a, V3 b, V3 c, V3 d, SKPaint paint)
        {
            path.Reset();
            path.MoveTo(Project(a, cx, cy, scale));
            path.LineTo(Project(b, cx, cy, scale));
            path.LineTo(Project(c, cx, cy, scale));
            path.LineTo(Project(d, cx, cy, scale));
            path.Close();
            canvas.DrawPath(path, paint);
            canvas.DrawPath(path, edgePaint);
        }

        Quad(bot[0], bot[1], top[1], top[0], sidePaint);
        Quad(bot[1], bot[2], top[2], top[1], darkPaint);
        Quad(bot[2], bot[3], top[3], top[2], sidePaint);
        Quad(bot[3], bot[0], top[0], top[3], darkPaint);
        Quad(top[0], top[1], top[2], top[3], topPaint);
        // No stand: the dish floats (matches the hero concept).
    }

    // ---- beam -------------------------------------------------------------

    private static SKColor Dim(SKColor c, float f) =>
        new((byte)(c.Red * f), (byte)(c.Green * f), (byte)(c.Blue * f), c.Alpha);

    private static SKColor BeamColor(int beam, byte alpha) => beam switch
    {
        BeamWarn => new SKColor(232, 180, 79, alpha),
        BeamBad => new SKColor(229, 107, 107, alpha),
        _ => new SKColor(240, 244, 250, alpha),
    };

    private static void DrawBeamHalo(SKCanvas canvas, SKPoint anchor, SKPoint tip,
        float scale, int beam, double timeSec)
    {
        float pulse = (float)(1 + 0.06 * Math.Sin(timeSec * 2.2));
        float len = Distance(anchor, tip);
        if (len < 1) return;
        // Perpendicular for the tapered quad.
        float dx = (tip.X - anchor.X) / len, dy = (tip.Y - anchor.Y) / len;
        float nx = -dy, ny = dx;
        float wBot = scale * 0.42f * pulse, wTop = scale * 0.10f * pulse;
        using var paint = new SKPaint { IsAntialias = true };
        paint.Shader = SKShader.CreateLinearGradient(
            anchor, tip,
            new[] { BeamColor(beam, 90), BeamColor(beam, 0) },
            SKShaderTileMode.Clamp);
        var path = new SKPath();
        path.MoveTo(anchor.X - nx * wBot, anchor.Y - ny * wBot);
        path.LineTo(tip.X - nx * wTop, tip.Y - ny * wTop);
        path.LineTo(tip.X + nx * wTop, tip.Y + ny * wTop);
        path.LineTo(anchor.X + nx * wBot, anchor.Y + ny * wBot);
        path.Close();
        canvas.DrawPath(path, paint);
        paint.Shader?.Dispose();
    }

    private static void DrawBeamCore(SKCanvas canvas, SKPoint anchor, SKPoint tip,
        float scale, double timeSec)
    {
        float pulse = (float)(1 + 0.10 * Math.Sin(timeSec * 2.2 + 0.6));
        float len = Distance(anchor, tip);
        if (len < 1) return;
        float dx = (tip.X - anchor.X) / len, dy = (tip.Y - anchor.Y) / len;
        float nx = -dy, ny = dx;
        float wBot = scale * 0.10f * pulse, wTop = scale * 0.022f * pulse;
        using var paint = new SKPaint { IsAntialias = true };
        paint.Shader = SKShader.CreateLinearGradient(
            anchor, tip,
            new[] { new SKColor(255, 255, 255, 235), new SKColor(255, 255, 255, 0) },
            SKShaderTileMode.Clamp);
        var path = new SKPath();
        path.MoveTo(anchor.X - nx * wBot, anchor.Y - ny * wBot);
        path.LineTo(tip.X - nx * wTop, tip.Y - ny * wTop);
        path.LineTo(tip.X + nx * wTop, tip.Y + ny * wTop);
        path.LineTo(anchor.X + nx * wBot, anchor.Y + ny * wBot);
        path.Close();
        canvas.DrawPath(path, paint);
        paint.Shader?.Dispose();
    }

    private static void DrawSatellite(SKCanvas canvas, SKPoint anchor, SKPoint tip,
        float scale, double timeSec)
    {
        double cycle = 6.0;
        double f = (timeSec % cycle) / cycle; // 0 base -> 1 tip
        float x = anchor.X + (float)((tip.X - anchor.X) * f);
        float y = anchor.Y + (float)((tip.Y - anchor.Y) * f);
        float alpha = (float)Math.Sin(f * Math.PI); // fade in/out at ends
        if (alpha <= 0.01) return;
        using var paint = new SKPaint { IsAntialias = true, Color = new SKColor(255, 255, 255, (byte)(220 * alpha)) };
        canvas.DrawCircle(x, y, Math.Max(1.5f, scale * 0.022f), paint);
    }

    private static float Distance(SKPoint a, SKPoint b)
    {
        float dx = b.X - a.X, dy = b.Y - a.Y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }
}
