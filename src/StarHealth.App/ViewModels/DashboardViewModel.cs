using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using StarHealth.Core.Abstractions;
using StarHealth.Core.Localization;
using StarHealth.Core.Models;
using StarHealth.Core.Services;
using StarHealth.Data.Grpc;

namespace StarHealth.App.ViewModels;

public sealed record AlertRow(string Message, bool IsGood);
public sealed record WedgeRow(string Label, double BarPct, string ValueText);
public sealed record TimelineRow(DateTimeOffset Timestamp, string TimeText, double DurationSec, string DurationText, string Text, bool IsOutage);

/// <summary>
/// Dashboard state. All dish access goes through <see cref="DishPollingService"/>;
/// this class only formats. No WinUI-only APIs except DispatcherQueue +
/// Visibility/Brush for binding, which are identical under Uno's WinUI flavor.
/// All strings flow through <see cref="Text"/> (es/en); XAML binds static text
/// via Tr() so a language switch refreshes every binding at once.
/// </summary>
public sealed partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly Func<DishEndpointOptions, IDishClient> _clientFactory;
    private readonly IDishClient _fallbackClient;
    private DishPollingService _poller;
    private IDisposable? _primaryClient;
    private readonly DispatcherQueue _dq = DispatcherQueue.GetForCurrentThread();
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private List<DishEndpointOptions> _endpoints = new();
    private DishEndpointOptions _activeEndpoint = new();

    /// <summary>Set by the shell: tag of the page to open from the alignment CTA.</summary>
    public Action<string>? Navigate { get; set; }

    /// <summary>XAML static-text lookup: Text="{x:Bind Vm.Tr('nav.status')}".</summary>
    public string Tr(string key) => Text.Get(key);

    public void ChangeLanguage(string lang)
    {
        try { Windows.Storage.ApplicationData.Current.LocalSettings.Values["lang"] = lang; }
        catch { }
        Text.Set(lang);
    }

#pragma warning disable MVVMTK0045
    [ObservableProperty] private string headerTitle = Text.Get("misc.plan");
    [ObservableProperty] private string headerSub = "Mini";
    [ObservableProperty] private string statusText = Text.Get("misc.connecting");
    [ObservableProperty] private string statusDetail = Text.Get("misc.looking");
    [ObservableProperty] private string badgeText = "···";
    [ObservableProperty] private SolidColorBrush badgeBrush = new(Microsoft.UI.Colors.Gray);
    [ObservableProperty] private bool dishReachable;
    [ObservableProperty] private string dishReachableText = "—";
    [ObservableProperty] private string downText = "—";
    [ObservableProperty] private string upText = "—";
    [ObservableProperty] private string latencyText = "—";
    [ObservableProperty] private string dropText = "—";
    [ObservableProperty] private string snrText = "—";
    [ObservableProperty] private string pingSuccessText = "—";
    [ObservableProperty] private string medianLatencyText = "—";
    [ObservableProperty] private List<double> pingSeries = new();
    [ObservableProperty] private List<double> latencySeries = new();
    [ObservableProperty] private List<double> powerSeries = new();
    [ObservableProperty] private double powerMax = 60;
    [ObservableProperty] private bool showBriefEvents = true;
    [ObservableProperty] private double obstructionPct;
    [ObservableProperty] private string obstructionText = "—";
    [ObservableProperty] private string prolongedText = "";
    [ObservableProperty] private List<WedgeRow> wedgeRows = new();
    [ObservableProperty] private Visibility wedgesNoteVisibility = Visibility.Collapsed;
    [ObservableProperty] private string alignmentTitle = "—";
    [ObservableProperty] private string alignmentSub = "";
    [ObservableProperty] private string alignmentTargetText = "";
    [ObservableProperty] private string alignmentNoticeText = "";
    [ObservableProperty] private Visibility alignmentNoticeVisibility = Visibility.Collapsed;
    [ObservableProperty] private string powerText = "—";
    [ObservableProperty] private string powerNote = "";
    [ObservableProperty] private string hardwareText = "—";
    [ObservableProperty] private string softwareText = "—";
    [ObservableProperty] private string softwareUpdateText = "—";
    [ObservableProperty] private string dishIdText = "—";
    [ObservableProperty] private string uptimeText = "—";
    [ObservableProperty] private string rebootText = "—";
    [ObservableProperty] private string batteryText = "";
    [ObservableProperty] private Visibility batteryVisibility = Visibility.Collapsed;
    [ObservableProperty] private string locationText = "—";
    [ObservableProperty] private string usageText = "—";
    [ObservableProperty] private string outageSummary = Text.Get("misc.out.none");
    [ObservableProperty] private string ethText = "—";
    [ObservableProperty] private string configSnowText = "—";
    [ObservableProperty] private string configPowerSaveText = "—";
    [ObservableProperty] private string configLocationText = "—";
    [ObservableProperty] private int mapRows;
    [ObservableProperty] private int mapCols;
    [ObservableProperty] private IList<float> mapSnr = new List<float>();
    [ObservableProperty] private string mapStatusText = Text.Get("obst.map.empty");
    [ObservableProperty] private Visibility mapVisibility = Visibility.Collapsed;
    [ObservableProperty] private string lastUpdatedText = "";
    [ObservableProperty] private string lastErrorText = "";
    [ObservableProperty] private Visibility errorVisibility = Visibility.Collapsed;
    [ObservableProperty] private Visibility alertsVisibility = Visibility.Collapsed;
    [ObservableProperty] private Visibility noAlertsVisibility = Visibility.Visible;
    [ObservableProperty] private IReadOnlyList<ThroughputSample> samples = new List<ThroughputSample>();
    [ObservableProperty] private double chartMax = 100;
    [ObservableProperty] private string planName = LoadPlan();
    [ObservableProperty] private string speedResultText = Text.Get("spd.none");
    [ObservableProperty] private string speedButtonText = Text.Get("spd.start");
    [ObservableProperty] private bool testing;
#pragma warning restore MVVMTK0045

    public ObservableCollection<AlertRow> Alerts { get; } = new();
    public ObservableCollection<TimelineRow> Timeline { get; } = new();
    public ObservableCollection<string> DishNames { get; } = new();

    public bool HasMultipleDishes => _endpoints.Count > 1;

    private bool _syncingEditor;
    private List<TimelineRow> _timelineAll = new();

    public string EndpointText => _activeEndpoint.EndpointText;

#pragma warning disable MVVMTK0045
    [ObservableProperty] private string editName = "";
    [ObservableProperty] private string editHost = "";
    [ObservableProperty] private string editPort = "";
    [ObservableProperty] private string editorErrorText = "";
    [ObservableProperty] private Visibility editorErrorVisibility = Visibility.Collapsed;
    [ObservableProperty] private string selectedDishName = "";
#pragma warning restore MVVMTK0045

    public bool CanTest => !Testing;

    public DashboardViewModel(Func<DishEndpointOptions, IDishClient> clientFactory, IDishClient fallback)
    {
        _clientFactory = clientFactory;
        _fallbackClient = fallback;
        try
        {
            var l = Windows.Storage.ApplicationData.Current.LocalSettings.Values["lang"] as string;
            if (l == "en" || l == "es") Text.Set(l);
        }
        catch { }
        LoadEndpoints();
        _poller = CreatePoller(_activeEndpoint);
        HeaderTitle = PlanName;
        Text.Changed += OnLangChanged;
    }

    private void OnLangChanged() => OnPropertyChanged(string.Empty);

    // ---- dish fleet (multi-dish endpoints + switcher) --------------------

    private void LoadEndpoints()
    {
        _endpoints = new List<DishEndpointOptions> { new("192.168.100.1", 9200, Text.Get("dish.default.name")) };
        string activeName = "";
        try
        {
            var raw = Windows.Storage.ApplicationData.Current.LocalSettings.Values["dishEndpoints"] as string;
            var saved = Windows.Storage.ApplicationData.Current.LocalSettings.Values["activeDish"] as string;
            if (!string.IsNullOrWhiteSpace(raw))
            {
                var list = System.Text.Json.JsonSerializer.Deserialize<List<DishEndpointOptions>>(raw);
                if (list is { Count: > 0 }) _endpoints = list;
            }
            activeName = saved ?? "";
        }
        catch { }
        _activeEndpoint = _endpoints.FirstOrDefault(e => e.Name == activeName) ?? _endpoints[0];
        RefreshDishNames();
        PopulateEditor(_activeEndpoint);
    }

    private void SaveEndpoints()
    {
        try
        {
            var settings = Windows.Storage.ApplicationData.Current.LocalSettings.Values;
            settings["dishEndpoints"] = System.Text.Json.JsonSerializer.Serialize(_endpoints);
            settings["activeDish"] = _activeEndpoint.Name;
        }
        catch { }
    }

    private void RefreshDishNames()
    {
        DishNames.Clear();
        foreach (var e in _endpoints) DishNames.Add(e.Name);
        OnPropertyChanged(nameof(HasMultipleDishes));
    }

    private void PopulateEditor(DishEndpointOptions ep)
    {
        _syncingEditor = true;
        try
        {
            EditName = ep.Name;
            EditHost = ep.Host;
            EditPort = ep.DishPort.ToString();
            SelectedDishName = ep.Name;
            EditorErrorText = "";
            EditorErrorVisibility = Visibility.Collapsed;
        }
        finally { _syncingEditor = false; }
    }

    private DishPollingService CreatePoller(DishEndpointOptions ep)
    {
        var primary = _clientFactory(ep);
        _primaryClient = primary as IDisposable;
        return new DishPollingService(primary, _fallbackClient);
    }

    private void SwitchTo(DishEndpointOptions ep)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _loop = null;
        _primaryClient?.Dispose();
        _primaryClient = null;
        _activeEndpoint = ep;
        _poller = CreatePoller(ep);
        SaveEndpoints();
        OnPropertyChanged(nameof(EndpointText));
        OnPropertyChanged(nameof(HasMultipleDishes));
        Start();
    }

    partial void OnSelectedDishNameChanged(string value)
    {
        if (_syncingEditor) return;
        var ep = _endpoints.FirstOrDefault(e => e.Name == value);
        if (ep is null || ep.Name == _activeEndpoint.Name)
        {
            if (ep is not null) PopulateEditor(ep);
            return;
        }
        PopulateEditor(ep);
        SwitchTo(ep);
    }

    [RelayCommand]
    private void AddDish()
    {
        if (!ReadEditor(out var ep)) return;
        if (_endpoints.Any(e => e.Name == ep.Name))
        {
            EditorError(Text.Get("dishes.err.name"));
            return;
        }
        _endpoints.Add(ep);
        RefreshDishNames();
        PopulateEditor(ep);
        SwitchTo(ep);
    }

    [RelayCommand]
    private void SaveDish()
    {
        if (!ReadEditor(out var ep)) return;
        int idx = _endpoints.FindIndex(e => e.Name == _activeEndpoint.Name);
        if (idx < 0) return;
        if (_endpoints.Any(e => e.Name == ep.Name && e.Name != _activeEndpoint.Name))
        {
            EditorError(Text.Get("dishes.err.name"));
            return;
        }
        _endpoints[idx] = ep;
        RefreshDishNames();
        PopulateEditor(ep);
        SwitchTo(ep);
    }

    [RelayCommand]
    private void RemoveDish()
    {
        if (_endpoints.Count <= 1)
        {
            EditorError(Text.Get("dishes.err.last"));
            return;
        }
        _endpoints.RemoveAll(e => e.Name == _activeEndpoint.Name);
        var next = _endpoints[0];
        RefreshDishNames();
        PopulateEditor(next);
        SwitchTo(next);
    }

    private bool ReadEditor(out DishEndpointOptions ep)
    {
        var name = EditName.Trim();
        var host = EditHost.Trim();
        if (string.IsNullOrWhiteSpace(name)) name = Text.Get("dish.default.name");
        if (!DishEndpointValidation.TryParsePort(EditPort, out int port))
        {
            EditorError(Text.Get("dishes.err.port"));
            ep = new DishEndpointOptions();
            return false;
        }
        if (!DishEndpointValidation.Validate(host, port, out _))
        {
            EditorError(Text.Get("dishes.err.host"));
            ep = new DishEndpointOptions();
            return false;
        }
        ep = new DishEndpointOptions(host, port, name);
        EditorErrorText = "";
        EditorErrorVisibility = Visibility.Collapsed;
        return true;
    }

    private void EditorError(string message)
    {
        EditorErrorText = message;
        EditorErrorVisibility = Visibility.Visible;
    }

    public void Start()
    {
        if (_loop is not null) return;
        _cts = new CancellationTokenSource();
        _loop = LoopAsync(_cts.Token);
    }

    public void Dispose()
    {
        Text.Changed -= OnLangChanged;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _loop = null;
        _primaryClient?.Dispose();
        _primaryClient = null;
    }

    partial void OnPlanNameChanged(string value)
    {
        HeaderTitle = string.IsNullOrWhiteSpace(value) ? Text.Get("misc.plan") : value.Trim();
        try { Windows.Storage.ApplicationData.Current.LocalSettings.Values["planName"] = HeaderTitle; }
        catch { }
    }

    partial void OnTestingChanged(bool value)
    {
        SpeedButtonText = value ? Text.Get("spd.running") : Text.Get("spd.start");
        RunSpeedTestCommand.NotifyCanExecuteChanged();
    }

    partial void OnShowBriefEventsChanged(bool value) => RefreshTimeline();

    private void RefreshTimeline()
    {
        var list = _timelineAll
            .Where(r => ShowBriefEvents || r.IsOutage || r.DurationSec >= 1)
            .ToList();
        // Steady state must produce zero collection traffic: ListView/ItemsControl
        // re-realize containers on any change notification, which reads as flicker.
        var sig = string.Join("|", list.Select(r => $"{r.Timestamp.Ticks}:{r.Text}"));
        if (sig == _timelineSig) return;
        _timelineSig = sig;
        SyncRows(Timeline, list);
    }

    private string _timelineSig = "";

    [RelayCommand]
    private void Align() => Navigate?.Invoke("Alineacion");

    [RelayCommand]
    private async Task RefreshMapAsync()
    {
        MapStatusText = Text.Get("obst.map.loading");
        try
        {
            var map = await _poller.GetObstructionMapAsync().ConfigureAwait(false);
            if (map is null || map.Snr.Length == 0)
            {
                _dq.TryEnqueue(() =>
                {
                    MapStatusText = Text.Get("obst.map.fail");
                    MapVisibility = Visibility.Collapsed;
                });
                return;
            }
            _dq.TryEnqueue(() =>
            {
                MapRows = map.Rows;
                MapCols = map.Cols;
                MapSnr = map.Snr;
                MapStatusText = $"{map.Rows}×{map.Cols}";
                MapVisibility = Visibility.Visible;
            });
        }
        catch (Exception ex)
        {
            _dq.TryEnqueue(() =>
            {
                MapStatusText = Text.Get("obst.map.fail") + " " + ex.Message;
                MapVisibility = Visibility.Collapsed;
            });
        }
    }

    [RelayCommand(CanExecute = nameof(CanTest))]
    private async Task RunSpeedTestAsync()
    {
        Testing = true;
        SpeedResultText = Text.Get("spd.running");
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            const long bytes = 25_000_000;
            var sw = Stopwatch.StartNew();
            var data = await http.GetByteArrayAsync(
                $"https://speed.cloudflare.com/__down?bytes={bytes}").ConfigureAwait(false);
            sw.Stop();
            double mbps = data.Length * 8 / 1e6 / Math.Max(0.1, sw.Elapsed.TotalSeconds);
            _dq.TryEnqueue(() => SpeedResultText =
                $"{mbps:F0} Mbps · 25 MB / {sw.Elapsed.TotalSeconds:F1} s");
        }
        catch (Exception ex)
        {
            _dq.TryEnqueue(() => SpeedResultText = Text.Get("spd.fail") + ex.Message);
        }
        finally
        {
            _dq.TryEnqueue(() => Testing = false);
        }
    }

    private static string LoadPlan()
    {
        try
        {
            return Windows.Storage.ApplicationData.Current.LocalSettings.Values["planName"] as string
                ?? Text.Get("misc.plan");
        }
        catch { return Text.Get("misc.plan"); }
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        await PollAsync(ct).ConfigureAwait(false);
        while (!ct.IsCancellationRequested)
        {
            try { await timer.WaitForNextTickAsync(ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            await PollAsync(ct).ConfigureAwait(false);
        }
    }

    private async Task PollAsync(CancellationToken ct)
    {
        try { await _poller.PollOnceAsync(ct).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        catch { /* fallback snapshot still delivered via Latest */ }
        var snap = _poller.Latest;
        if (snap is null) return;
        var fallback = _poller.UsingFallback;
        var err = _poller.LastError;
        _dq.TryEnqueue(() => Apply(snap, fallback, err));
    }

    private static string StateText(DishState s) => Text.Get("state." + s switch
    {
        DishState.Connected => "connected",
        DishState.Searching => "searching",
        DishState.Booting => "booting",
        DishState.Stowed => "stowed",
        DishState.Sleeping => "sleeping",
        DishState.Obstructed => "obstructed",
        DishState.NoSatellites => "nosats",
        DishState.NoSignal => "nosignal",
        DishState.ThermalShutdown => "thermal",
        DishState.Offline => "offline",
        _ => "unknown",
    });

    private static string CauseText(OutageCause c) => Text.Get("cause." + c switch
    {
        OutageCause.Obstructed => "obstructed",
        OutageCause.NoSatellites => "nosats",
        OutageCause.ThermalShutdown => "thermal",
        OutageCause.ThermalThrottle => "throttle",
        OutageCause.SoftwareUpdate => "update",
        OutageCause.NetworkIssue => "network",
        OutageCause.PowerDip => "power",
        OutageCause.Booting => "booting",
        OutageCause.Stowed => "stowed",
        OutageCause.Sleeping => "sleeping",
        OutageCause.SkySearch => "skysearch",
        OutageCause.ActuatorActivity => "actuator",
        OutageCause.CableTest => "cable",
        OutageCause.Inhibited => "inhibited",
        _ => "unknown",
    });

    private void Apply(DishSnapshot s, bool fallback, string? error)
    {
        HeaderSub = StarHealth.Core.Formatting.Format.FriendlyHardware(s.Device.HardwareVersion);
        StatusText = StateText(s.State);
        StatusDetail = s.FromDemo
            ? Text.Get("misc.demo")
            : $"{Text.Uptime(s.Uptime)} · {s.Device.DishId}";
        if (HasMultipleDishes) StatusDetail += $" · {_activeEndpoint.Name}";
        DishReachable = !fallback;
        DishReachableText = fallback ? Text.Get("misc.reach.no") : Text.Get("misc.reach.yes");

        BadgeText = s.FromDemo ? "DEMO" : "LIVE";
        BadgeBrush = new SolidColorBrush(s.FromDemo
            ? Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xE8, 0xB4, 0x4F)
            : Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x6F, 0xD5, 0x98));

        DownText = s.Throughput.DownMbps >= 100
            ? $"{s.Throughput.DownMbps:F0}" : $"{s.Throughput.DownMbps:F1}";
        UpText = $"{s.Throughput.UpMbps:F1}";
        LatencyText = $"{s.Latency.PopPingMs:F0}";
        DropText = $"{s.Latency.DropRate * 100:F1}%";
        SnrText = s.Signal.SnrDb > 0 ? $"{s.Signal.SnrDb:F1} dB"
            : s.SignalQuality is > 0 ? $"{s.SignalQuality * 100:F0} %" : "—";

        var ring = s.History;
        double success = s.Stats is { Samples: > 0 } st
            ? 1 - st.LossRatio
            : ring.Count == 0 ? 1 : ring.Average(p => 1 - p.DropRate);
        double? median = s.Stats?.MedianLatencyMs ?? Median(ring.Select(p => p.LatencyMs));
        PingSuccessText = $"{success * 100:F1}";
        MedianLatencyText = median is null ? "—" : $"{median:F0}";
        PingSeries = ring.Select(p => (1 - p.DropRate) * 100).ToList();
        LatencySeries = ring.Select(p => p.LatencyMs).ToList();
        PowerSeries = ring.Select(p => p.PowerW ?? 0).ToList();
        PowerMax = Math.Max(10, PowerSeries.DefaultIfEmpty(10).Max() * 1.2);

        ObstructionPct = s.Obstruction.FractionObstructed * 100;
        ObstructionText = $"{s.Obstruction.FractionObstructed * 100:F1}% · " +
            $"{Text.Duration(TimeSpan.FromSeconds(s.Obstruction.ObstructedSeconds))} / 24 h";
        ProlongedText = (s.Obstruction.AvgProlongedDurationSec, s.Obstruction.AvgProlongedIntervalSec) switch
        {
            (double d, double i) =>
                $"{Text.Get("obst.prolonged")} {Text.Duration(TimeSpan.FromSeconds(d))} · {Text.Get("obst.every")} {Text.Duration(TimeSpan.FromSeconds(i))}",
            _ => Text.Get("obst.prolonged.no"),
        };
        var wedges = s.Obstruction.WedgeFractions;
        double wmax = wedges.Length == 0 ? 0 : wedges.Max();
        var nextWedges = wedges.Select((v, i) => new WedgeRow(
            $"{i * 30}°–{(i + 1) * 30}°",
            wmax <= 0 ? 0 : v / wmax * 100,
            $"{v * 100:F1} %")).ToList();
        if (!WedgeRows.SequenceEqual(nextWedges)) WedgeRows = nextWedges;
        WedgesNoteVisibility = wedges.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        AlignmentTitle = s.Alignment.Summary;
        AlignmentSub = s.Alignment.TiltDeg is double tilt
            ? $"{Text.Get("align.tilt", $"{tilt:F1}")}" + (s.Alignment.MastNearVertical == false ? Text.Get("align.mast.suffix") : "")
            : s.Alignment.BoresightAzimuthDeg is null ? Text.Get("align.fixed") : "";
        AlignmentNoticeText = s.AlignmentNotice ?? "";
        AlignmentNoticeVisibility = s.AlignmentNotice is null ? Visibility.Collapsed : Visibility.Visible;
        AlignmentTargetText = (s.Alignment.DesiredAzimuthDeg, s.Alignment.DesiredElevationDeg) switch
        {
            (double da, double de) => Text.Get("align.target", $"{da:F1}", $"{de:F1}") +
                (s.Alignment.MisalignmentDeg is double m ? Text.Get("align.dev", $"{m:F0}") : ""),
            _ => "",
        };

        PowerText = s.Power.Watts is null ? "n/d"
            : s.Power.IsEstimate ? $"~{s.Power.Watts:F0} W (est.)" : $"{s.Power.Watts:F1} W";
        PowerNote = s.Power.Source ?? "";

        HardwareText = s.Device.HardwareVersion;
        SoftwareText = s.Device.SoftwareVersion;
        SoftwareUpdateText = s.SoftwareUpdate ?? "—";
        DishIdText = s.Device.DishId;
        UptimeText = Text.Uptime(s.Uptime);
        RebootText = s.RebootReason ?? "—";
        if (s.Battery?.StateOfChargePct is uint soc)
        {
            BatteryText = $"{soc} % · " + (s.Battery.IsCharging ? Text.Get("dish.batt.charging") + " · " : "") + s.Battery.PowerSource;
            BatteryVisibility = Visibility.Visible;
        }
        else BatteryVisibility = Visibility.Collapsed;
        LocationText = s.Location is { Latitude: not null, Longitude: not null } loc
            ? $"{loc.Latitude:F4}, {loc.Longitude:F4}" +
              (loc.GpsSatellites is int sats ? $" · {sats} sats" : "")
            : Text.Get("misc.loc.gated");
        ulong dn = s.Stats?.DownloadBytes ?? s.Throughput.TotalDownBytes ?? 0;
        ulong up = s.Stats?.UploadBytes ?? s.Throughput.TotalUpBytes ?? 0;
        UsageText = dn > 0 ? $"↓ {dn / 1e9:F0} GB · ↑ {up / 1e9:F1} GB" : Text.Get("misc.usage.no");
        ConfigSnowText = s.Config?.SnowMeltMode ?? "—";
        ConfigPowerSaveText = s.Config is null ? "—"
            : s.Config.PowerSaveMode ? Text.Get("cfg.eq.on") : Text.Get("cfg.eq.off");
        ConfigLocationText = s.Config?.LocationRequestMode ?? "—";

        _timelineAll = s.RecentOutages
            .Select(o => new TimelineRow(
                o.Start.ToLocalTime(),
                o.Start.LocalDateTime.ToString("HH:mm:ss"),
                o.Duration.TotalSeconds,
                Text.Duration(o.Duration),
                CauseText(o.Cause),
                true))
            .Concat((s.RecentEvents ?? Enumerable.Empty<DishEvent>())
                .Select(e => new TimelineRow(
                    e.Timestamp.ToLocalTime(),
                    e.Timestamp.LocalDateTime.ToString("HH:mm:ss"),
                    e.Duration.TotalSeconds,
                    Text.Duration(e.Duration),
                    $"{e.Reason} · {e.Severity}",
                    false)))
            .OrderByDescending(r => r.Timestamp)
            .Take(30)
            .ToList();
        RefreshTimeline();
        var totalOut = TimeSpan.FromSeconds(s.RecentOutages.Sum(o => o.Duration.TotalSeconds));
        OutageSummary = s.RecentOutages.Count == 0
            ? s.Stats?.LongestFullDropRunSec is > 0 and var longest
                ? Text.Get("misc.out.session", longest)
                : Text.Get("misc.out.none")
            : Text.Get("misc.out.summary", s.RecentOutages.Count, Text.Duration(totalOut));

        SyncRows(
            Alerts,
            s.ActiveAlerts.Where(a => a.Active)
                .Select(a => new AlertRow(a.Message, IsGoodAlert(a.Kind))).ToList());
        AlertsVisibility = Alerts.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        NoAlertsVisibility = Alerts.Count > 0 ? Visibility.Collapsed : Visibility.Visible;

        EthText = s.EthSpeedMbps is int eth ? $"{eth} Mbps" : Text.Get("net.eth.none");

        Samples = s.History;
        ChartMax = Math.Max(50, s.History.Select(p => p.DownMbps).DefaultIfEmpty(50).Max() * 1.2);

        LastUpdatedText = Text.Get("misc.updated") + " " + s.Timestamp.LocalDateTime.ToString("HH:mm:ss");
        LastErrorText = error ?? "";
        ErrorVisibility = string.IsNullOrEmpty(error) ? Visibility.Collapsed : Visibility.Visible;
    }

    private static double? Median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        return sorted.Length == 0 ? null : sorted[sorted.Length / 2];
    }

    /// <summary>Informational/benign alerts render green instead of amber.</summary>
    private static bool IsGoodAlert(string kind) => kind switch
    {
        "is_heating" or "is_power_save_idle" or "software_update_reboot" => true,
        _ => false,
    };

    /// <summary>
    /// Keyed in-place sync: reuses existing row instances so the ListViews
    /// don't rebuild (no flicker), inserts genuinely new rows at their
    /// sorted position (newest lands on top), drops the rest.
    /// </summary>
    private static void SyncRows<T>(ObservableCollection<T> current, List<T> next)
        where T : notnull
    {
        for (int i = 0; i < next.Count; i++)
        {
            if (i < current.Count && EqualityComparer<T>.Default.Equals(current[i], next[i]))
                continue;
            int j = -1;
            for (int k = i + 1; k < current.Count; k++)
                if (EqualityComparer<T>.Default.Equals(current[k], next[i])) { j = k; break; }
            if (j >= 0) current.Move(j, i);
            else current.Insert(i, next[i]);
        }
        while (current.Count > next.Count) current.RemoveAt(current.Count - 1);
    }
}
