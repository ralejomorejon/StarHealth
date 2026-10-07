using System.Net.Sockets;
using Google.Protobuf.Reflection;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Reflection.V1Alpha;
using StarHealth.Core.Abstractions;
using StarHealth.Core.Localization;
using StarHealth.Core.Models;

namespace StarHealth.Data.Grpc;

/// <summary>
/// Live dish client. Uses gRPC server reflection (the same trick as
/// sparky8512's dish_control.py) so no vendored .proto can go stale:
/// field numbers are discovered from the dish at connect time and only
/// stable field *names* are mapped. Field semantics were validated against
/// a real Mini (mini1_panda_prod1): state derives from `outage`, per-wedge
/// detail and status SNR are gone, power/usage/outages/event-log live in
/// `get_history`, outage timestamps are ns since the GPS epoch (1980-01-06),
/// `alignment_stats` carries actual vs desired boresight, and the
/// obstruction heatmap comes from `dish_get_obstruction_map`.
/// Endpoint: plaintext HTTP/2 (h2c) at http://192.168.100.1:9200.
/// </summary>
public sealed class GrpcDishClient : IDishClient, IDisposable
{
    /// <summary>Offset between GPS epoch (dish clock) and Unix epoch, in ns.</summary>
    private const long GpsToUnixNs = 315_964_800_000_000_000L;

    public string DisplayName => $"Dish @{_options.Address}";
    public bool IsDemo => false;

    private readonly DishEndpointOptions _options;
    private readonly GrpcChannel _channel;
    private readonly List<ThroughputSample> _ring = new();
    private readonly List<OutageEvent> _outages = new();
    private DateTimeOffset? _obstructedSince;

    // Discovered schema (cached after first reflection call).
    private readonly SemaphoreSlim _schemaLock = new(1, 1);
    private Dictionary<string, DescriptorProto>? _messages;
    private Dictionary<string, Dictionary<long, string>>? _enums;
    private DescriptorProto? _reqSchema;
    private DescriptorProto? _resSchema;

    public GrpcDishClient(DishEndpointOptions? options = null)
    {
        _options = options ?? new DishEndpointOptions();
        _channel = GrpcChannel.ForAddress(_options.Address, new GrpcChannelOptions
        {
            Credentials = ChannelCredentials.Insecure,
            HttpHandler = new SocketsHttpHandler { EnableMultipleHttp2Connections = true },
        });
    }

    public async Task<DishSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;

        var (status, statusSchema) = await HandleAsync("get_status", "dish_get_status", ct).ConfigureAwait(false);
        var bootTime = now - TimeSpan.FromSeconds(
            Nested(status, statusSchema, "device_state")?.GetLong("uptime_s") ?? 0);

        HistoryStats? stats = null;
        List<OutageEvent>? dishOutages = null;
        List<DishEvent>? dishEvents = null;
        try
        {
            var (history, historySchema) = await HandleAsync("get_history", "dish_get_history", ct).ConfigureAwait(false);
            (stats, dishOutages, dishEvents) = ExtractHistory(historySchema, history, now, bootTime);
        }
        catch { /* history is best-effort; the status ring still feeds charts */ }

        // Diagnostics: self-test, stowed flag, config-independent location backup.
        ProtoCodec.Msg? diag = null;
        DescriptorProto? diagSchema = null;
        try { (diag, diagSchema) = await HandleAsync("get_diagnostics", "dish_get_diagnostics", ct).ConfigureAwait(false); }
        catch { }

        LocationInfo? location = null;
        try { location = await GetLocationAsync(ct).ConfigureAwait(false); }
        catch { /* gated by the dish privacy toggle; null means "not shared" */ }
        if (location is null && diag is not null && diagSchema is not null)
            location = DiagnosticsLocation(diag, diagSchema);

        DishConfigInfo? config = null;
        try
        {
            var (cfg, cfgSchema) = await HandleAsync("dish_get_config", "dish_get_config", ct).ConfigureAwait(false);
            config = ParseConfig(cfg, cfgSchema);
        }
        catch { }

        return Map(now, bootTime, status, statusSchema, stats, dishOutages, dishEvents,
            diag, diagSchema, location, config);
    }

    public async Task<ObstructionMapData?> GetObstructionMapAsync(CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct).ConfigureAwait(false);
        var (msg, _) = await HandleAsync("dish_get_obstruction_map", "dish_get_obstruction_map", ct)
            .ConfigureAwait(false);
        int rows = (int)msg.GetLong("num_rows");
        int cols = (int)msg.GetLong("num_cols");
        var snr = msg.GetDoubles("snr").Select(v => (float)v).ToArray();
        if (rows <= 0 || cols <= 0 || snr.Length == 0) return null;
        return new ObstructionMapData(rows, cols, snr, (float)msg.GetDouble("min_elevation_deg"));
    }

    // ---- transport ----------------------------------------------------

    private async Task<(ProtoCodec.Msg Msg, DescriptorProto Schema)> HandleAsync(string reqField, string resField, CancellationToken ct)
    {
        int reqNum = Field(_reqSchema!, reqField).Number;
        var resFieldDesc = Field(_resSchema!, resField);

        var method = new Method<byte[], byte[]>(
            MethodType.Unary, "SpaceX.API.Device.Device", "Handle",
            Marshallers.Create(b => b, b => b),
            Marshallers.Create(b => b, b => b));

        using var call = _channel.CreateCallInvoker().AsyncUnaryCall(
            method, host: null,
            new CallOptions(deadline: DateTime.UtcNow.AddSeconds(6), cancellationToken: ct),
            ProtoCodec.EmptyRequest(reqNum));

        byte[] bytes = await call.ResponseAsync.ConfigureAwait(false);
        var top = ProtoCodec.Parse(bytes, _resSchema!);

        if (top.TryGetValue(resField, out var nested) && nested is byte[] raw &&
            _messages!.TryGetValue(TrimDot(resFieldDesc.TypeName), out var schema))
        {
            return (ProtoCodec.Parse(raw, schema), schema);
        }
        throw new InvalidOperationException($"Dish response had no '{resField}'.");
    }

    private async Task<LocationInfo?> GetLocationAsync(CancellationToken ct)
    {
        // NOTE: response field is literally `get_location` (see starlink_grpc.py).
        var (msg, schema) = await HandleAsync("get_location", "get_location", ct).ConfigureAwait(false);
        var lla = Nested(msg, schema, "lla");
        if (lla is null) return null;
        var lat = TryDouble(lla, "lat");
        var lon = TryDouble(lla, "lon");
        if (lat is null || lon is null) return null;
        return new LocationInfo(lat, lon, TryDouble(lla, "alt"), null, true);
    }

    // ---- mapping -------------------------------------------------------

    private DishSnapshot Map(DateTimeOffset now, DateTimeOffset bootTime,
        ProtoCodec.Msg s, DescriptorProto sSchema,
        HistoryStats? stats, List<OutageEvent>? dishOutages, List<DishEvent>? dishEvents,
        ProtoCodec.Msg? diag, DescriptorProto? diagSchema,
        LocationInfo? location, DishConfigInfo? config)
    {
        var dev = Nested(s, sSchema, "device_info");
        var devState = Nested(s, sSchema, "device_state");
        var obst = Nested(s, sSchema, "obstruction_stats");
        var alerts = Nested(s, sSchema, "alerts");
        var gps = Nested(s, sSchema, "gps_stats");
        var align = Nested(s, sSchema, "alignment_stats");
        var battery = Nested(s, sSchema, "battery_stats");

        // State derives from the `outage` field: absent => CONNECTED.
        var outage = Nested(s, sSchema, "outage");
        DishState state;
        if (outage is null)
        {
            state = DishState.Connected;
        }
        else
        {
            var causeName = EnumName(OutageCauseType(sSchema), outage.GetLong("cause", -1));
            state = causeName switch
            {
                "NO_SCHEDULE" or "SKY_SEARCH" => DishState.Searching,
                "BOOTING" => DishState.Booting,
                "STOWED" => DishState.Stowed,
                "SLEEPING" => DishState.Sleeping,
                "OBSTRUCTED" => DishState.Obstructed,
                "NO_SATS" => DishState.NoSatellites,
                "THERMAL_SHUTDOWN" => DishState.ThermalShutdown,
                "NO_DOWNLINK" or "NO_PINGS" => DishState.NoSignal,
                _ => DishState.Unknown,
            };
        }

        // Diagnostics can override: stowed flag, self-test / disablement alerts.
        var extraAlerts = new List<DishAlert>();
        if (diag is not null)
        {
            if (diag.TryGetBool("stowed") == true) state = DishState.Stowed;
            if (diagSchema is not null)
            {
                var selfTest = EnumFieldName(diagSchema, diag, "hardware_self_test");
                if (selfTest is not null && selfTest != "PASS" && selfTest != "TEST_RESULT_PASS" &&
                    selfTest != "UNKNOWN" && selfTest != "TEST_RESULT_UNKNOWN")
                    extraAlerts.Add(new DishAlert("self_test", $"{Text.Get("dish.selftest")}: {selfTest}.", true));
                var dis = EnumFieldName(diagSchema, diag, "disablement_code");
                if (dis is not null && dis != "UNKNOWN" && dis != "0" && !dis.EndsWith("OK"))
                    extraAlerts.Add(new DishAlert("disablement", $"{Text.Get("dish.disablement")}: {dis}.", true));
            }
        }

        double downBps = s.GetDouble("downlink_throughput_bps");
        double upBps = s.GetDouble("uplink_throughput_bps");
        double down = downBps / 1e6, up = upBps / 1e6;
        double latency = s.GetDouble("pop_ping_latency_ms");
        double drop = s.GetDouble("pop_ping_drop_rate", s.GetDouble("pop_ping_drop_rate_1m"));
        double snr = s.GetDouble("snr"); // obsolete on current firmware; 0 means "not reported"
        float quality = (float)s.GetDouble("signal_quality");

        bool obstructed = state == DishState.Connected &&
            (obst?.TryGetBool("currently_obstructed") == true || drop > 0.2);
        if (obstructed && _obstructedSince is null) _obstructedSince = now;
        if (!obstructed)
        {
            if (_obstructedSince is not null)
            {
                _outages.Add(new OutageEvent(_obstructedSince.Value, now - _obstructedSince.Value,
                    OutageCause.Obstructed));
                while (_outages.Count > 30) _outages.RemoveAt(0);
                _obstructedSince = null;
            }
        }
        else state = DishState.Obstructed;

        _ring.Add(new ThroughputSample(now, down, up, latency, drop, obstructed,
            stats?.LatestPowerW is > 0 ? stats.LatestPowerW : null));
        while (_ring.Count > 900) _ring.RemoveAt(0);

        // Interruptions: prefer the dish's own timestamped list.
        List<OutageEvent> recentOutages;
        if (dishOutages is { Count: > 0 })
            recentOutages = dishOutages.OrderByDescending(o => o.Start).Take(10).ToList();
        else
        {
            recentOutages = _outages.OrderByDescending(o => o.Start).Take(10).ToList();
            if (obstructed && _obstructedSince is not null)
                recentOutages.Insert(0, new OutageEvent(_obstructedSince.Value,
                    now - _obstructedSince.Value, OutageCause.Obstructed));
        }

        double fraction = obst?.GetDouble("fraction_obstructed") ?? 0;
        double validS = obst?.GetDouble("valid_s") ?? 0;
        double obstS = fraction * validS;
        var wedges = FirstDoubles(obst, "wedge_fraction_obstructed", "wedges_fraction_obstructed",
            "raw_wedge_fraction_obstructed", "raw_wedges_fraction_obstructed",
            "wedge_abs_fraction_obstructed").ToArray();

        var activeAlerts = new List<DishAlert>(extraAlerts);
        if (alerts is not null)
            foreach (var kv in alerts)
                if (kv.Value is bool b && b)
                    activeAlerts.Add(new DishAlert(kv.Key, AlertText(kv.Key), true));

        // Alignment: actual vs desired boresight from alignment_stats.
        double? az = TryDouble(s, "boresight_azimuth_deg") ?? TryDouble(align, "boresight_azimuth_deg");
        double? el = TryDouble(s, "boresight_elevation_deg") ?? TryDouble(align, "boresight_elevation_deg");
        double? tilt = align is null ? null : TryDouble(align, "tilt_angle_deg");
        double? desAz = align is null ? null : TryDouble(align, "desired_boresight_azimuth_deg");
        double? desEl = align is null ? null : TryDouble(align, "desired_boresight_elevation_deg");
        double? mis = (az, el, desAz, desEl) is (not null, not null, not null, not null)
            ? AngleBetween(az.Value, el.Value, desAz.Value, desEl.Value) : null;
        string summary = (az, el) switch
        {
            (not null, not null) => $"Acimut {az:F1}° · Elevación {el:F1}°" +
                (tilt is not null ? $" · Inclinación {tilt:F1}°" : ""),
            _ => "La antena no informa orientación",
        };

        string? alignmentNotice = null;
        if (mis is >= 10)
            alignmentNotice = Text.Get("dish.notice.mis", $"{mis:F0}");
        else if (alerts?.TryGetBool("mast_not_near_vertical") == true)
            alignmentNotice = Text.Get("dish.align.mast");
        else if (alerts?.TryGetBool("motors_stuck") == true)
            alignmentNotice = Text.Get("dish.align.motors");

        long uptimeS = devState?.GetLong("uptime_s") ?? 0;
        string swFull = dev?.GetString("software_version", "?") ?? "?";
        string swShort = swFull.Length <= 12 ? swFull : swFull[..12];

        double? latestW = stats?.LatestPowerW is > 0 ? stats.LatestPowerW : null;
        if (latestW is null && s.GetDouble("power_in") > 0) latestW = s.GetDouble("power_in");

        string? update = SoftwareUpdateText(s, sSchema);

        ulong? totalDown = stats?.DownloadBytes > 0 ? stats.DownloadBytes : null;
        ulong? totalUp = stats?.UploadBytes > 0 ? stats.UploadBytes : null;

        long gpsRaw = gps?.GetLong("gps_sats", -1) ?? -1;
        int? gpsSats = gpsRaw >= 0 ? (int)gpsRaw : null;

        int eth = (int)s.GetLong("eth_speed_mbps", -1);
        string? reboot = EnumFieldName(sSchema, s, "reboot_reason");
        if (reboot is "REBOOT_REASON_NONE" or "UNKNOWN" or "0") reboot = null;

        BatteryInfo? batteryInfo = null;
        if (battery is not null && sSchema is not null)
        {
            long soc = battery.GetLong("state_of_charge", -1);
            var src = EnumFieldName(
                _messages!.Values.FirstOrDefault(m => m.Field.Any(f => f.Name == "battery_stats")) ?? sSchema,
                battery, "power_source") ?? "desconocida";
            batteryInfo = new BatteryInfo(
                soc >= 0 ? (uint)soc : null,
                battery.TryGetBool("is_charging") == true,
                src switch
                {
                    "USBC" => Text.Get("dish.batt.usbc"),
                    "BATTERY" => Text.Get("dish.batt.battery"),
                    "USBC_AND_BATTERY" => Text.Get("dish.batt.both"),
                    _ => Text.Get("dish.batt.unknown"),
                });
        }

        return new DishSnapshot(
            Timestamp: now,
            FromDemo: false,
            State: state,
            Device: new DeviceInfo(
                dev?.GetString("id", "?") ?? "?",
                dev?.GetString("hardware_version", "?") ?? "?",
                swShort,
                dev?.GetString("country_code", "?") ?? "?"),
            Throughput: new ThroughputInfo(down, up, totalDown, totalUp),
            Latency: new LatencyInfo(latency, drop),
            Signal: new SignalInfo(snr),
            Alignment: new AlignmentInfo(az, el, tilt, null, summary, desAz, desEl, mis),
            Obstruction: new ObstructionInfo(
                fraction, validS, obstS, wedges, new double[0, 0],
                obst is null ? null : TryDouble(obst, "avg_prolonged_obstruction_duration_s"),
                obst is null ? null : TryDouble(obst, "avg_prolonged_obstruction_interval_s")),
            Power: new PowerInfo(latestW, IsEstimate: false,
                latestW is null ? Text.Get("dish.power.none") : Text.Get("dish.power.measured")),
            Location: location,
            RecentOutages: recentOutages,
            ActiveAlerts: activeAlerts,
            History: _ring.ToList(),
            Uptime: TimeSpan.FromSeconds(uptimeS),
            Stats: stats,
            SoftwareUpdate: update,
            AlignmentNotice: alignmentNotice,
            EthSpeedMbps: eth > 0 ? eth : null,
            SignalQuality: quality > 0 ? quality : null,
            RebootReason: reboot,
            Battery: batteryInfo,
            Config: config,
            RecentEvents: dishEvents);
    }

    private static double AngleBetween(double az1, double el1, double az2, double el2)
    {
        static double R(double d) => d * Math.PI / 180;
        double dot = Math.Cos(R(el1)) * Math.Cos(R(az1)) * Math.Cos(R(el2)) * Math.Cos(R(az2))
            + Math.Cos(R(el1)) * Math.Sin(R(az1)) * Math.Cos(R(el2)) * Math.Sin(R(az2))
            + Math.Sin(R(el1)) * Math.Sin(R(el2));
        return Math.Acos(Math.Clamp(dot, -1, 1)) * 180 / Math.PI;
    }

    private static OutageCause MapCause(string? name) => name switch
    {
        "OBSTRUCTED" => OutageCause.Obstructed,
        "NO_SATS" or "NO_SATELLITES" or "NO_SCHEDULE" => OutageCause.NoSatellites,
        "THERMAL_SHUTDOWN" => OutageCause.ThermalShutdown,
        "NO_DOWNLINK" or "NO_PINGS" => OutageCause.NetworkIssue,
        "BOOTING" => OutageCause.Booting,
        "STOWED" => OutageCause.Stowed,
        "SLEEPING" => OutageCause.Sleeping,
        "SKY_SEARCH" => OutageCause.SkySearch,
        "ACTUATOR_ACTIVITY" => OutageCause.ActuatorActivity,
        "CABLE_TEST" => OutageCause.CableTest,
        "INHIBIT_RF" => OutageCause.Inhibited,
        _ => OutageCause.Unknown,
    };

    private string? SoftwareUpdateText(ProtoCodec.Msg s, DescriptorProto sSchema)
    {
        var field = sSchema.Field.FirstOrDefault(f => f.Name == "software_update_state");
        string? state = field is not null && !string.IsNullOrEmpty(field.TypeName)
            ? EnumName(field.TypeName, s.GetLong("software_update_state", -1))
            : null;
        var upd = Nested(s, sSchema, "software_update_stats");
        double? progress = upd is null ? null : TryDouble(upd, "software_update_progress");
        bool rebootReady = s.TryGetBool("swupdate_reboot_ready") == true;
        if (state is null && progress is null && !rebootReady) return null;
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(state) && state != "IDLE" && state != "SOFTWARE_UPDATE_STATE_UNKNOWN")
            parts.Add(state switch
            {
                "REBOOT_REQUIRED" => Text.Get("dish.update.ready"),
                "FETCHING" => Text.Get("dish.update.fetch"),
                "WRITING" => Text.Get("dish.update.write"),
                "FAULTED" => Text.Get("dish.update.fail"),
                "DISABLED" => Text.Get("dish.update.disabled"),
                _ => $"Actualización: {state}",
            });
        if (progress is > 0 and < 1) parts.Add($"{progress:P0}");
        if (rebootReady) parts.Add(Text.Get("dish.update.reboot"));
        return parts.Count == 0 ? Text.Get("dish.update.ok") : string.Join(" · ", parts);
    }

    private LocationInfo? DiagnosticsLocation(ProtoCodec.Msg diag, DescriptorProto diagSchema)
    {
        var loc = Nested(diag, diagSchema, "location");
        if (loc?.TryGetBool("enabled") != true) return null;
        var lat = TryDouble(loc, "latitude");
        var lon = TryDouble(loc, "longitude");
        if (lat is null || lon is null || (lat == 0 && lon == 0)) return null;
        return new LocationInfo(lat, lon, TryDouble(loc, "altitude_meters"), null, true);
    }

    private DishConfigInfo? ParseConfig(ProtoCodec.Msg cfg, DescriptorProto cfgSchema)
    {
        var dish = Nested(cfg, cfgSchema, "dish_config");
        if (dish is null) return null;
        var dishSchema = cfgSchema.Field.FirstOrDefault(f => f.Name == "dish_config")?.TypeName is string tn
            && _messages!.TryGetValue(TrimDot(tn), out var ds) ? ds : cfgSchema;
        return new DishConfigInfo(
            EnumFieldName(dishSchema, dish, "snow_melt_mode") switch
            {
                "AUTO" => "Automático",
                "ALWAYS_ON" => "Siempre encendido",
                "ALWAYS_OFF" => "Apagado",
                var other => other,
            },
            dish.TryGetBool("power_save_mode") == true,
            EnumFieldName(dishSchema, dish, "location_request_mode") switch
            {
                "NONE" => "No solicitada",
                "LOCAL" => "Local",
                var other => other,
            });
    }

    // ---- history (dish-computed aggregates + bulk arrays + outages + events) --

    private (HistoryStats? Stats, List<OutageEvent> Outages, List<DishEvent> Events) ExtractHistory(
        DescriptorProto historySchema, ProtoCodec.Msg history, DateTimeOffset now, DateTimeOffset bootTime)
    {
        var acc = new Dictionary<string, List<object?>>();
        Collect(historySchema, history, acc);

        // Bulk per-second arrays: backfill the chart ring (oldest -> newest).
        var drops = Doubles(acc, "pop_ping_drop_rate");
        var lats = Doubles(acc, "pop_ping_latency_ms");
        var downs = Doubles(acc, "downlink_throughput_bps");
        var ups = Doubles(acc, "uplink_throughput_bps");
        var pwrs = Doubles(acc, "power_in");
        if (drops.Count > 1)
        {
            _ring.Clear();
            int n = drops.Count;
            for (int i = 0; i < n; i++)
            {
                double lt = i < lats.Count ? lats[i] : 0;
                _ring.Add(new ThroughputSample(
                    now.AddSeconds(i - (n - 1)),
                    (i < downs.Count ? downs[i] : 0) / 1e6,
                    (i < ups.Count ? ups[i] : 0) / 1e6,
                    lt, drops[i], false,
                    i < pwrs.Count && pwrs[i] > 0 ? pwrs[i] : null));
            }
            while (_ring.Count > 900) _ring.RemoveAt(0);
        }

        // Timestamped outages (this firmware reports them; older ones used run stats).
        var outages = new List<OutageEvent>();
        if (_messages!.TryGetValue("SpaceX.API.Device.DishOutage", out var outageSchema) &&
            acc.TryGetValue("outages", out var rawOutages))
        {
            foreach (var raw in rawOutages)
            {
                if (raw is not byte[] bytes) continue;
                try
                {
                    var o = ProtoCodec.Parse(bytes, outageSchema);
                    var causeField = outageSchema.Field.FirstOrDefault(f => f.Name == "cause");
                    var cause = causeField is null ? null
                        : EnumName(causeField.TypeName, o.GetLong("cause", -1));
                    long startNs = o.GetLong("start_timestamp_ns", 0);
                    double durS = Convert.ToDouble(o.TryGetValue("duration_ns", out var d) ? d ?? 0 : 0) / 1e9;
                    if (startNs <= 0 || durS <= 0 || durS > 86400) continue;
                    outages.Add(new OutageEvent(TsFromNs(startNs, now, bootTime),
                        TimeSpan.FromSeconds(durS), MapCause(cause)));
                }
                catch { }
            }
        }

        // Event log (UX events; flattened by Collect into "events").
        var events = new List<DishEvent>();
        if (_messages!.TryGetValue("SpaceX.API.Device.UXEvent", out var evSchema) &&
            acc.TryGetValue("events", out var rawEvs))
        {
            foreach (var raw in rawEvs)
            {
                if (raw is not byte[] bytes) continue;
                try
                {
                    var m = ProtoCodec.Parse(bytes, evSchema);
                    long startNs = m.GetLong("start_timestamp_ns", 0);
                    if (startNs <= 0) continue;
                    double durS = Convert.ToDouble(m.TryGetValue("duration_ns", out var d) ? d ?? 0 : 0) / 1e9;
                    events.Add(new DishEvent(
                        TsFromNs(startNs, now, bootTime),
                        TimeSpan.FromSeconds(Math.Min(Math.Max(durS, 0), 86400)),
                        PrettyEvent(EnumFieldName(evSchema, m, "severity") ?? "?"),
                        PrettyEvent(EnumFieldName(evSchema, m, "reason") ?? "?")));
                }
                catch { }
            }
        }
        events = events.OrderByDescending(e => e.Timestamp).Take(20).ToList();

        int samples = (int)FirstLong(acc, "samples", drops.Count);
        var powers = Doubles(acc, "power_in");
        double latestP = FirstDouble(acc, "latest_power",
            powers.Count > 0 ? powers[^1] : 0);
        double meanP = FirstDoubleOrNull(acc, "mean_power")
            ?? (powers.Count > 0 ? powers.Average() : 0);
        double maxP = FirstDouble(acc, "max_power",
            powers.Count > 0 ? powers.Max() : 0);
        double energy = FirstDouble(acc, "total_energy", 0);

        var decAll = Doubles(acc, "deciles_all_ping_latency");
        var decFull = Doubles(acc, "deciles_full_ping_latency");
        double? median = decFull.Count > 5 ? decFull[5] : decAll.Count > 5 ? decAll[5]
            : lats.Count > 0 ? lats.OrderBy(v => v).ElementAt(lats.Count / 2) : null;
        double? mean = FirstDoubleOrNull(acc, "mean_full_ping_latency")
            ?? FirstDoubleOrNull(acc, "mean_all_ping_latency")
            ?? (lats.Count > 0 ? lats.Average() : null);

        double totalDrop = FirstDouble(acc, "total_ping_drop",
            drops.Count > 0 ? drops.Sum() : 0);
        if (samples == 0 && drops.Count == 0 && outages.Count == 0 && events.Count == 0
            && latestP == 0 && median is null)
            return (null, outages, events);

        int longest = 0;
        var runMin = Doubles(acc, "run_minutes");
        for (int i = runMin.Count - 1; i >= 0; i--)
            if (runMin[i] > 0) { longest = (i + 2) * 60; break; }
        if (longest == 0)
        {
            var runSec = Doubles(acc, "run_seconds");
            for (int i = runSec.Count - 1; i >= 0; i--)
                if (runSec[i] > 0) { longest = i + 2; break; }
        }

        var st = new HistoryStats(
            Samples: samples,
            LossRatio: samples > 0 ? totalDrop / samples
                : drops.Count > 0 ? drops.Average() : 0,
            FullDropSamples: (int)FirstLong(acc, "count_full_ping_drop",
                drops.Count(p => p >= 1)),
            MedianLatencyMs: median,
            MeanLatencyMs: mean,
            DownloadBytes: (ulong)Math.Max(0, FirstLong(acc, "download_usage", 0)),
            UploadBytes: (ulong)Math.Max(0, FirstLong(acc, "upload_usage", 0)),
            LatestPowerW: latestP > 0 ? latestP : null,
            MeanPowerW: meanP > 0 ? meanP : null,
            MaxPowerW: maxP > 0 ? maxP : null,
            TotalEnergyKwh: energy > 0 ? energy : null,
            LongestFullDropRunSec: longest);
        return (st, outages, events);
    }

    /// <summary>Dish timestamps are ns since the GPS epoch; sanity-checked.</summary>
    private static DateTimeOffset TsFromNs(long ns, DateTimeOffset now, DateTimeOffset bootTime)
    {
        try
        {
            var dto = DateTimeOffset.FromUnixTimeMilliseconds((ns + GpsToUnixNs) / 1_000_000);
            if (dto > new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero) && dto <= now.AddHours(1))
                return dto.ToLocalTime();
        }
        catch { }
        return bootTime.AddSeconds(ns / 1e9);
    }

    private void Collect(DescriptorProto schema, ProtoCodec.Msg msg, Dictionary<string, List<object?>> acc)
    {
        foreach (var kv in msg)
        {
            if (kv.Value is byte[] raw)
            {
                var f = schema.Field.FirstOrDefault(x => x.Name == kv.Key);
                if (f?.Type == FieldDescriptorProto.Types.Type.Message &&
                    !string.IsNullOrEmpty(f.TypeName) &&
                    _messages!.TryGetValue(TrimDot(f.TypeName), out var sub))
                {
                    Collect(sub, ProtoCodec.Parse(raw, sub), acc);
                    continue;
                }
            }
            if (!acc.TryGetValue(kv.Key, out var list)) acc[kv.Key] = list = new List<object?>();
            if (kv.Value is List<object?> packed) list.AddRange(packed);
            else list.Add(kv.Value);
        }
    }

    private static List<double> Doubles(Dictionary<string, List<object?>> acc, string name)
    {
        var out_ = new List<double>();
        if (acc.TryGetValue(name, out var list))
            foreach (var v in list)
                try { if (v is not null) out_.Add(Convert.ToDouble(v)); } catch { }
        return out_;
    }

    private static double FirstDouble(Dictionary<string, List<object?>> acc, string name, double def)
        => FirstDoubleOrNull(acc, name) ?? def;

    private static double? FirstDoubleOrNull(Dictionary<string, List<object?>> acc, string name)
    {
        if (acc.TryGetValue(name, out var list))
            foreach (var v in list)
                try { if (v is not null) return Convert.ToDouble(v); } catch { }
        return null;
    }

    private static long FirstLong(Dictionary<string, List<object?>> acc, string name, long def)
    {
        if (acc.TryGetValue(name, out var list))
            foreach (var v in list)
                try { if (v is not null) return Convert.ToInt64(v); } catch { }
        return def;
    }

    // ---- schema helpers --------------------------------------------------

    private ProtoCodec.Msg? Nested(ProtoCodec.Msg parent, DescriptorProto parentSchema, string name)
    {
        if (!parent.TryGetValue(name, out var v) || v is not byte[] raw) return null;
        var field = parentSchema.Field.FirstOrDefault(f => f.Name == name);
        if (field is null || string.IsNullOrEmpty(field.TypeName)) return null;
        return _messages!.TryGetValue(TrimDot(field.TypeName), out var nested)
            ? ProtoCodec.Parse(raw, nested) : null;
    }

    private static List<double> FirstDoubles(ProtoCodec.Msg? m, params string[] names)
    {
        if (m is null) return new List<double>();
        foreach (var n in names)
        {
            var d = m.GetDoubles(n);
            if (d.Count > 0) return d;
        }
        return new List<double>();
    }

    private static double? TryDouble(ProtoCodec.Msg? m, string name)
    {
        if (m is null || !m.TryGetValue(name, out var v) || v is null) return null;
        try
        {
            double d = Convert.ToDouble(v);
            return double.IsNaN(d) || double.IsInfinity(d) ? null : d;
        }
        catch { return null; }
    }

    private string? EnumFieldName(DescriptorProto parentSchema, ProtoCodec.Msg parent, string fieldName)
    {
        var f = parentSchema.Field.FirstOrDefault(x => x.Name == fieldName);
        if (f is null || string.IsNullOrEmpty(f.TypeName)) return null;
        if (!parent.TryGetValue(fieldName, out var v) || v is null) return null;
        try { return EnumName(f.TypeName, Convert.ToInt64(v)); }
        catch { return null; }
    }

    private static string PrettyEvent(string raw)
    {
        string t = raw;
        if (t.StartsWith("EVENT_SEVERITY_")) t = t["EVENT_SEVERITY_".Length..];
        if (t.StartsWith("EVENT_REASON_")) t = t["EVENT_REASON_".Length..];
        return t switch
        {
            "WARNING" => "Aviso",
            "INFO" => "Info",
            "ERROR" => "Error",
            "CRITICAL" => "Crítico",
            "OUTAGE_NO_PINGS" => "corte: sin pings",
            "OUTAGE_NO_DOWNLINK" => "corte: sin bajada",
            "OUTAGE_OBSTRUCTED" => "corte: obstrucción",
            "OUTAGE_NO_SATS" => "corte: sin satélites",
            _ => t.Replace('_', ' ').ToLowerInvariant(),
        };
    }

    private static string AlertText(string kind) => kind switch
    {
        "motors_stuck" => Text.Get("dish.alert.motors"),
        "thermal_throttle" => Text.Get("dish.alert.throttle"),
        "thermal_shutdown" => Text.Get("dish.alert.thermal"),
        "mast_not_near_vertical" => Text.Get("dish.alert.mast"),
        "unexpected_location" => Text.Get("dish.alert.location"),
        "slow_ethernet_speeds" => Text.Get("dish.alert.eth"),
        "slow_ethernet_speeds_100" => Text.Get("dish.alert.eth100"),
        "no_ethernet_link" => Text.Get("dish.alert.noeth"),
        "roaming" => Text.Get("dish.alert.roaming"),
        "is_heating" => Text.Get("dish.alert.heating"),
        "is_power_save_idle" => Text.Get("dish.alert.powersave"),
        "power_supply_thermal_throttle" => Text.Get("dish.alert.psu"),
        "low_motor_current" => Text.Get("dish.alert.lowmotor"),
        "lower_signal_than_predicted" => Text.Get("dish.alert.lowsignal"),
        "dish_water_detected" => Text.Get("dish.alert.water"),
        "router_water_detected" => Text.Get("dish.alert.rwater"),
        "install_pending" => Text.Get("dish.alert.install"),
        "obstruction_map_reset" => Text.Get("dish.alert.mapreset"),
        "dbf_telem_stale" => Text.Get("dish.alert.stale"),
        "software_update_reboot" => Text.Get("dish.alert.swreboot"),
        "low_power" => Text.Get("dish.alert.lowpower"),
        _ => kind.Replace('_', ' ') + ".",
    };

    private string? OutageCauseType(DescriptorProto statusSchema)
    {
        var outageField = statusSchema.Field.FirstOrDefault(f => f.Name == "outage");
        if (outageField is null || !_messages!.TryGetValue(TrimDot(outageField.TypeName), out var outageMsg))
            return null;
        return outageMsg.Field.FirstOrDefault(f => f.Name == "cause")?.TypeName;
    }

    private string? EnumName(string? typeName, long number)
    {
        if (typeName is null || _enums is null) return null;
        return _enums.TryGetValue(TrimDot(typeName), out var map) && map.TryGetValue(number, out var name)
            ? name : null;
    }

    private static FieldDescriptorProto Field(DescriptorProto schema, string name)
        => schema.Field.FirstOrDefault(f => f.Name == name)
            ?? throw new InvalidOperationException($"Dish schema has no field '{name}'.");

    private static string TrimDot(string s) => s.TrimStart('.');

    // ---- schema discovery via server reflection ------------------------

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        if (_messages is not null) return;
        await _schemaLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_messages is not null) return;
            var files = await FetchDescriptorsAsync("SpaceX.API.Device.Device", ct).ConfigureAwait(false);

            _messages = new Dictionary<string, DescriptorProto>();
            _enums = new Dictionary<string, Dictionary<long, string>>();
            foreach (var f in files)
            {
                string pkg = TrimDot(f.Package);
                foreach (var m in f.MessageType)
                    AddMessageRecursive($"{pkg}.{m.Name}", m);
                foreach (var e in f.EnumType)
                    _enums[$"{pkg}.{e.Name}"] = e.Value.ToDictionary(v => (long)v.Number, v => v.Name);
            }

            string reqType = "", resType = "";
            foreach (var f in files)
                foreach (var svc in f.Service)
                    if (TrimDot($"{f.Package}.{svc.Name}") == "SpaceX.API.Device.Device")
                        foreach (var m in svc.Method)
                            if (m.Name == "Handle") { reqType = TrimDot(m.InputType); resType = TrimDot(m.OutputType); }

            if (!_messages.TryGetValue(reqType, out _reqSchema) ||
                !_messages.TryGetValue(resType, out _resSchema))
                throw new InvalidOperationException("Device/Handle schema not found via reflection.");

            void AddMessageRecursive(string fullName, DescriptorProto m)
            {
                _messages[fullName] = m;
                foreach (var nested in m.NestedType)
                    AddMessageRecursive($"{fullName}.{nested.Name}", nested);
                foreach (var e in m.EnumType)
                    _enums[$"{fullName}.{e.Name}"] = e.Value.ToDictionary(v => (long)v.Number, v => v.Name);
            }
        }
        finally { _schemaLock.Release(); }
    }

    private async Task<List<FileDescriptorProto>> FetchDescriptorsAsync(string symbol, CancellationToken ct)
    {
        var client = new ServerReflection.ServerReflectionClient(_channel);
        using var call = client.ServerReflectionInfo(cancellationToken: ct);
        var seen = new Dictionary<string, FileDescriptorProto>();
        var queue = new Queue<ServerReflectionRequest>();
        queue.Enqueue(new ServerReflectionRequest { FileContainingSymbol = symbol });

        // Bound the walk: dish protos are small, 32 files is plenty.
        for (int i = 0; i < 32 && queue.Count > 0; i++)
        {
            await call.RequestStream.WriteAsync(queue.Dequeue()).ConfigureAwait(false);
            if (!await call.ResponseStream.MoveNext(ct).ConfigureAwait(false)) break;
            var resp = call.ResponseStream.Current;
            if (resp.MessageResponseCase != ServerReflectionResponse.MessageResponseOneofCase.FileDescriptorResponse)
                continue;
            foreach (var bytes in resp.FileDescriptorResponse.FileDescriptorProto)
            {
                var file = FileDescriptorProto.Parser.ParseFrom(bytes);
                if (seen.ContainsKey(file.Name)) continue;
                seen[file.Name] = file;
                foreach (var dep in file.Dependency)
                    if (!seen.ContainsKey(dep))
                        queue.Enqueue(new ServerReflectionRequest { FileByFilename = dep });
            }
        }
        try { await call.RequestStream.CompleteAsync().ConfigureAwait(false); } catch { }
        return seen.Values.ToList();
    }

    public void Dispose()
    {
        _channel.Dispose();
        _schemaLock.Dispose();
    }
}
