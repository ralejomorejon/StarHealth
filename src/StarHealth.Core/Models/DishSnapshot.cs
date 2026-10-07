namespace StarHealth.Core.Models;

/// <summary>High-level link state, mirrors Starlink app header.</summary>
public enum DishState
{
    Unknown,
    Connected,
    Searching,
    Booting,
    Stowed,
    Sleeping,
    ThermalShutdown,
    Obstructed,
    NoSatellites,
    NoSignal,
    Offline
}

public enum OutageCause
{
    Unknown,
    Obstructed,
    NoSatellites,
    ThermalShutdown,
    ThermalThrottle,
    SoftwareUpdate,
    NetworkIssue,
    PowerDip,
    Booting,
    Stowed,
    Sleeping,
    SkySearch,
    ActuatorActivity,
    CableTest,
    Inhibited
}

/// <summary>Everything the dashboard needs in one poll cycle.</summary>
public sealed record DishSnapshot(
    DateTimeOffset Timestamp,
    bool FromDemo,
    DishState State,
    DeviceInfo Device,
    ThroughputInfo Throughput,
    LatencyInfo Latency,
    SignalInfo Signal,
    AlignmentInfo Alignment,
    ObstructionInfo Obstruction,
    PowerInfo Power,
    LocationInfo? Location,
    IReadOnlyList<OutageEvent> RecentOutages,
    IReadOnlyList<DishAlert> ActiveAlerts,
    IReadOnlyList<ThroughputSample> History,
    TimeSpan Uptime,
    HistoryStats? Stats = null,
    string? SoftwareUpdate = null,
    string? AlignmentNotice = null,
    int? EthSpeedMbps = null,
    float? SignalQuality = null,
    string? RebootReason = null,
    BatteryInfo? Battery = null,
    DishConfigInfo? Config = null,
    IReadOnlyList<DishEvent>? RecentEvents = null);

public sealed record DeviceInfo(
    string DishId,
    string HardwareVersion,
    string SoftwareVersion,
    string CountryCode,
    string? PopId = null);

public sealed record ThroughputInfo(
    double DownMbps,
    double UpMbps,
    ulong? TotalDownBytes = null,
    ulong? TotalUpBytes = null);

public sealed record LatencyInfo(
    double PopPingMs,
    double DropRate, // 0..1
    double? LoadedLatencyMs = null,
    double? P50Ms = null,
    double? P95Ms = null);

public sealed record SignalInfo(double SnrDb);

/// <summary>
/// Actuated dishes report boresight az/el. Fixed (Gen3/Mini) dishes
/// report tilt derived from accelerometer. Null = dish didn't report it.
/// </summary>
public sealed record AlignmentInfo(
    double? BoresightAzimuthDeg,
    double? BoresightElevationDeg,
    double? TiltDeg,
    bool? MastNearVertical,
    string Summary,
    double? DesiredAzimuthDeg = null,
    double? DesiredElevationDeg = null,
    double? MisalignmentDeg = null);

public sealed record ObstructionInfo(
    double FractionObstructed,   // 0..1, last-24h window
    double ValidSeconds,
    double ObstructedSeconds,
    double[] WedgeFractions,    // 12 wedges, 0..1 each (empty: firmware no longer reports them)
    double[,] ObstructionMap,   // SNR grid for heatmap; empty if unavailable
    double? AvgProlongedDurationSec = null,
    double? AvgProlongedIntervalSec = null);

/// <summary>
/// Aggregates computed by the dish itself over its ~15-minute history buffer
/// (get_history stats). Null when the dish didn't return them.
/// </summary>
public sealed record HistoryStats(
    int Samples,
    double LossRatio,            // total_ping_drop / samples
    int FullDropSamples,
    double? MedianLatencyMs,     // deciles_full_ping_latency[5]
    double? MeanLatencyMs,
    ulong DownloadBytes,
    ulong UploadBytes,
    double? LatestPowerW,        // null/0 when hardware doesn't report power
    double? MeanPowerW,
    double? MaxPowerW,
    double? TotalEnergyKwh,
    int LongestFullDropRunSec);

public sealed record PowerInfo(
    double? Watts,               // null when dish doesn't expose it
    bool IsEstimate,
    string? Source = null);

public sealed record LocationInfo(
    double? Latitude,
    double? Longitude,
    double? AltitudeMeters,
    int? GpsSatellites,
    bool? Valid);

public sealed record ThroughputSample(
    DateTimeOffset Timestamp,
    double DownMbps,
    double UpMbps,
    double LatencyMs,
    double DropRate,
    bool Obstructed,
    double? PowerW = null);

public sealed record OutageEvent(
    DateTimeOffset Start,
    TimeSpan Duration,
    OutageCause Cause);

public sealed record DishAlert(
    string Kind,
    string Message,
    bool Active);

/// <summary>Mini battery/DC state (Mini hardware reports this in status).</summary>
public sealed record BatteryInfo(
    uint? StateOfChargePct,
    bool IsCharging,
    string PowerSource);

/// <summary>Dish-side config relevant for display (read-only here).</summary>
public sealed record DishConfigInfo(
    string? SnowMeltMode,
    bool PowerSaveMode,
    string? LocationRequestMode);

/// <summary>Timestamped event from the dish event log (history).</summary>
public sealed record DishEvent(
    DateTimeOffset Timestamp,
    TimeSpan Duration,
    string Severity,
    string Reason);

/// <summary>Obstruction heatmap grid (dish_get_obstruction_map).</summary>
public sealed record ObstructionMapData(
    int Rows,
    int Cols,
    float[] Snr,
    float MinElevationDeg);
