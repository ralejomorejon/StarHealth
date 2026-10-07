using StarHealth.Core.Models;

namespace StarHealth.Core.Formatting;

/// <summary>UI strings follow the Starlink app (Spanish, like the reference design).</summary>
public static class Format
{
    public static string Mbps(double v) => v >= 100 ? $"{v:F0} Mbps" : $"{v:F1} Mbps";
    public static string Ms(double v) => $"{v:F0} ms";
    public static string Pct(double f) => $"{f * 100:F1}%";
    public static string Watts(double? w, bool est)
        => w is null ? "n/d" : est ? $"~{w:F0} W (est.)" : $"{w:F1} W";
    public static string Duration(TimeSpan d)
        => d.TotalHours >= 1 ? $"{(int)d.TotalHours} h {d.Minutes} min"
            : d.TotalMinutes >= 1 ? $"{d.Minutes} min {d.Seconds} s" : $"{d.Seconds} s";
    public static string Uptime(TimeSpan u)
        => u.TotalDays >= 1 ? $"{(int)u.TotalDays} d {u.Hours} h" : Duration(u);

    public static string Cause(OutageCause c) => c switch
    {
        OutageCause.Obstructed => "Obstruida",
        OutageCause.NoSatellites => "Sin satélites",
        OutageCause.ThermalShutdown => "Apagado térmico",
        OutageCause.ThermalThrottle => "Límite térmico",
        OutageCause.SoftwareUpdate => "Actualización",
        OutageCause.NetworkIssue => "Red",
        OutageCause.PowerDip => "Energía",
        OutageCause.Booting => "Arranque",
        OutageCause.Stowed => "Guardada",
        OutageCause.Sleeping => "Reposo",
        OutageCause.SkySearch => "Búsqueda de cielo",
        OutageCause.ActuatorActivity => "Motores",
        OutageCause.CableTest => "Prueba de cable",
        OutageCause.Inhibited => "Inhibida",
        _ => "Desconocida",
    };

    public static string State(DishState s) => s switch
    {
        DishState.Connected => "En línea",
        DishState.Searching => "Buscando",
        DishState.Booting => "Iniciando",
        DishState.Stowed => "Guardada",
        DishState.Sleeping => "En reposo",
        DishState.Obstructed => "Obstruida",
        DishState.NoSatellites => "Sin satélites",
        DishState.NoSignal => "Sin señal",
        DishState.ThermalShutdown => "Apagado térmico",
        DishState.Offline => "Sin conexión",
        _ => "Desconocido",
    };

    /// <summary>
    /// Best-effort friendly model name from hardware_version. Heuristic:
    /// version strings are opaque revision codes, not marketing names.
    /// </summary>
    public static string FriendlyHardware(string hw)
    {
        var h = hw.ToLowerInvariant();
        if (h.Contains("mini")) return "Mini";
        if (h.Contains("high_performance") || h.Contains("hp")) return "High Performance";
        if (h.Contains("rev4") || h.Contains("rev3")) return Localization.Text.Get("hw.standard");
        if (h.Contains("rev2") || h.Contains("rev1")) return Localization.Text.Get("hw.round");
        return hw;
    }

    public static string WedgeLabel(int index) => $"{index * 30}°–{(index + 1) * 30}°";
}
