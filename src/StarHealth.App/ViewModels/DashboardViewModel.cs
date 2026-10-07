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
    private readonly StarHealth.Data.Store.HistoryStore _store;
    private DishPollingService _poller;
    private IDisposable? _primaryClient;
    private readonly DispatcherQueue _dq = DispatcherQueue.GetForCurrentThread();
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private List<DishEndpointOptions> _endpoints = new();
    private DishEndpointOptions _activeEndpoint = new();

    /// <summary>Set by the shell: tag of the page to open from the alignment CTA.</summary>
    public Action<string>? Navigate { get; set; }
    public Action<string>? TrayUpdate { get; set; }
    public event Action? TraySettingsChanged;

    private bool? _wasReachable;
    private readonly HashSet<string> _seenOutages = new();
    private bool _updateToasted;
    private string? _lastNotice;
    private DateTime _lastToast = DateTime.MinValue;

    partial void OnNotificationsEnabledChanged(bool value) => SaveBool("notify", value);
    partial void OnTrayEnabledChanged(bool value)
    {
        SaveBool("tray", value);
        TraySettingsChanged?.Invoke();
    }

    private static bool LoadBool(string key, bool def) => LocalData.GetBool(key, def);

    private static void SaveBool(string key, bool value) => LocalData.SetBool(key, value);

    /// <summary>XAML static-text lookup: Text="{x:Bind Vm.Tr('nav.status')}".</summary>
    public string Tr(string key) => Text.Get(key);

    public void ChangeLanguage(string lang)
    {
        LocalData.SetString("lang", lang);
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
    [ObservableProperty] private double heroTilt = 15;
    [ObservableProperty] private int heroBeam;
    [ObservableProperty] private bool notificationsEnabled = LoadBool("notify", true);
    [ObservableProperty] private bool trayEnabled = LoadBool("tray", true);
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
    [ObservableProperty] private int historyRange;
    [ObservableProperty] private IReadOnlyList<ThroughputSample> chartSamples = new List<ThroughputSample>();
    [ObservableProperty] private IReadOnlyList<ThroughputSample> samples = new List<ThroughputSample>();
    [ObservableProperty] private double chartMax = 100;
    [ObservableProperty] private string sla24Text = "—";
    [ObservableProperty] private string sla7Text = "—";
    [ObservableProperty] private string reportStatusText = "";
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
    private bool _hasSpeedResult;
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

    public DashboardViewModel(Func<DishEndpointOptions, IDishClient> clientFactory, IDishClient fallback, StarHealth.Data.Store.HistoryStore store)
    {
        _clientFactory = clientFactory;
        _fallbackClient = fallback;
        _store = store;
        try
        {
            var l = LocalData.GetString("lang");
            if (l == "en" || l == "es") Text.Set(l);
        }
        catch { }
        LoadEndpoints();
        _poller = CreatePoller(_activeEndpoint);
        ReseedStaticText();
        HeaderTitle = PlanName;
        Text.Changed += OnLangChanged;
    }

    private void OnLangChanged()
    {
        ReseedStaticText();
        OnPropertyChanged(string.Empty);
    }

    /// <summary>
    /// Properties that only refresh on their own trigger (plan edits, speed
    /// tests, map loads) would otherwise stay in the old language until that
    /// trigger fires. Everything poll-driven refreshes within 2 s on its own.
    /// </summary>
    private void ReseedStaticText()
    {
        if (PlanName is "Residencial" or "Residential") PlanName = Text.Get("misc.plan");
        else HeaderTitle = PlanName;
        SpeedButtonText = Testing ? Text.Get("spd.running") : Text.Get("spd.start");
        if (!_hasSpeedResult) SpeedResultText = Text.Get("spd.none");
    }

    // ---- dish fleet (multi-dish endpoints + switcher) --------------------

    private void LoadEndpoints()
    {
        _endpoints = new List<DishEndpointOptions> { new("192.168.100.1", 9200, Text.Get("dish.default.name")) };
        string activeName = "";
        try
        {
            var raw = LocalData.GetString("dishEndpoints");
            var saved = LocalData.GetString("activeDish");
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
            LocalData.SetString("dishEndpoints", System.Text.Json.JsonSerializer.Serialize(_endpoints));
            LocalData.SetString("activeDish", _activeEndpoint.Name);
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
        _ = RefreshHistoryAsync();
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
        _ = RefreshHistoryAsync();
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
        LocalData.SetString("planName", HeaderTitle);
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
            _hasSpeedResult = true;
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
            return LocalData.GetString("planName") ?? Text.Get("misc.plan");
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

    private int _pollCount;

    private async Task PollAsync(CancellationToken ct)
    {
        try { await _poller.PollOnceAsync(ct).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        catch { /* fallback snapshot still delivered via Latest */ }
        var snap = _poller.Latest;
        if (snap is null) return;
        var fallback = _poller.UsingFallback;
        var err = _poller.LastError;
        _ = _store.RecordAsync(_activeEndpoint.Name, snap);
        _dq.TryEnqueue(() => Apply(snap, fallback, err));
        if (++_pollCount % 10 == 0 && HistoryRange != 0)
            await RefreshHistoryAsync().ConfigureAwait(false);
    }

    partial void OnHistoryRangeChanged(int value) => _ = RefreshHistoryAsync();

    private static long HoursAgo(int h) => DateTimeOffset.UtcNow.AddHours(-h).ToUnixTimeMilliseconds();

    [RelayCommand]
    private async Task RefreshHistoryAsync()
    {
        var dish = _activeEndpoint.Name;
        try
        {
            if (HistoryRange == 0)
            {
                var ring = _poller.Latest?.History ?? new List<ThroughputSample>();
                _dq.TryEnqueue(() =>
                {
                    ChartSamples = ring;
                    ChartMax = Math.Max(50, ring.Select(p => p.DownMbps).DefaultIfEmpty(50).Max() * 1.2);
                });
            }
            else
            {
                long from = HoursAgo(HistoryRange == 1 ? 24 : 24 * 7);
                var rows = await _store.GetSeriesAsync(dish, from).ConfigureAwait(false);
                var list = rows.Select(r => new ThroughputSample(
                    DateTimeOffset.FromUnixTimeMilliseconds(r.Ts),
                    r.Down, r.Up, r.Lat, r.Drop, false, r.Power)).ToList();
                _dq.TryEnqueue(() =>
                {
                    ChartSamples = list;
                    ChartMax = Math.Max(50, list.Select(p => p.DownMbps).DefaultIfEmpty(50).Max() * 1.2);
                });
            }
            var sla24 = await _store.GetAvailabilityAsync(dish, HoursAgo(24)).ConfigureAwait(false);
            var sla7 = await _store.GetAvailabilityAsync(dish, HoursAgo(24 * 7)).ConfigureAwait(false);
            _dq.TryEnqueue(() =>
            {
                Sla24Text = $"{sla24.Ratio * 100:F2}% · {sla24.Samples}";
                Sla7Text = $"{sla7.Ratio * 100:F2}% · {sla7.Samples}";
            });
        }
        catch { /* history panel stays as-is on DB errors */ }
    }

    private string HistoryRangeLabel() => HistoryRange switch
    {
        1 => Text.Get("hist.range.24h"),
        2 => Text.Get("hist.range.7d"),
        _ => Text.Get("hist.range.24h"),
    };

    private long HistoryRangeFrom() => HistoryRange == 2 ? HoursAgo(24 * 7) : HoursAgo(24);

    [RelayCommand]
    private async Task CopyReportAsync()
    {
        try
        {
            var report = await _store.BuildReportAsync(
                _activeEndpoint.Name, HistoryRangeLabel(), HistoryRangeFrom()).ConfigureAwait(false);
            _dq.TryEnqueue(() =>
            {
                var pkg = new Windows.ApplicationModel.DataTransfer.DataPackage();
                pkg.SetText(report);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(pkg);
                ReportStatusText = Text.Get("rpt.copied");
            });
        }
        catch (Exception ex)
        {
            _dq.TryEnqueue(() => ReportStatusText = Text.Get("spd.fail") + ex.Message);
        }
    }

    [RelayCommand]
    private async Task SaveReportAsync()
    {
        try
        {
            var report = await _store.BuildReportAsync(
                _activeEndpoint.Name, HistoryRangeLabel(), HistoryRangeFrom()).ConfigureAwait(false);
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "StarHealth");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir,
                $"starhealth-informe-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            await File.WriteAllTextAsync(file, report).ConfigureAwait(false);
            _dq.TryEnqueue(() => ReportStatusText = Text.Get("rpt.saved") + file);
        }
        catch (Exception ex)
        {
            _dq.TryEnqueue(() => ReportStatusText = Text.Get("spd.fail") + ex.Message);
        }
    }

    private static string StateText(DishState s) => Text.State(s);

    private static string CauseText(OutageCause c) => Text.Cause(c);

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

        HeroTilt = s.Alignment.TiltDeg ?? 15;
        HeroBeam = s.State != DishState.Connected ? Controls.DishHeroScene.BeamBad
            : s.Obstruction.FractionObstructed >= 0.02 ? Controls.DishHeroScene.BeamWarn
            : Controls.DishHeroScene.BeamOk;

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
        if (HistoryRange == 0)
        {
            ChartSamples = s.History;
            ChartMax = Math.Max(50, s.History.Select(p => p.DownMbps).DefaultIfEmpty(50).Max() * 1.2);
        }

        LastUpdatedText = Text.Get("misc.updated") + " " + s.Timestamp.LocalDateTime.ToString("HH:mm:ss");
        LastErrorText = error ?? "";
        ErrorVisibility = string.IsNullOrEmpty(error) ? Visibility.Collapsed : Visibility.Visible;

        TrayUpdate?.Invoke($"StarHealth · {StatusText} · ↓{DownText} ↑{UpText}");
        EvaluateNotifications(s, fallback);
    }

    private void EvaluateNotifications(DishSnapshot s, bool fallback)
    {
        if (!NotificationsEnabled || s.FromDemo) return;
        bool reachable = !fallback;

        if (_wasReachable is bool was && was != reachable)
            ShowToast(Text.Get(reachable ? "toast.online.t" : "toast.offline.t"),
                Text.Get(reachable ? "toast.online.b" : "toast.offline.b"), force: true);
        _wasReachable = reachable;

        foreach (var o in s.RecentOutages)
        {
            string key = $"{o.Start.Ticks}:{o.Cause}";
            if (!_seenOutages.Add(key)) continue;
            if (_seenOutages.Count > 500) _seenOutages.Clear();
            if (o.Duration.TotalSeconds >= 5)
                ShowToast(Text.Get("toast.outage.t"),
                    $"{CauseText(o.Cause)} · {Text.Duration(o.Duration)} · {o.Start.LocalDateTime:HH:mm:ss}");
        }

        string okUpdate = Text.Get("dish.update.ok");
        if (!string.IsNullOrEmpty(s.SoftwareUpdate) && s.SoftwareUpdate != okUpdate && !_updateToasted)
        {
            _updateToasted = true;
            ShowToast(Text.Get("toast.update.t"), s.SoftwareUpdate, force: true);
        }
        else if (s.SoftwareUpdate == okUpdate) _updateToasted = false;

        if (s.AlignmentNotice is not null && s.AlignmentNotice != _lastNotice)
        {
            _lastNotice = s.AlignmentNotice;
            ShowToast(Text.Get("toast.align.t"), s.AlignmentNotice, force: true);
        }
        else if (s.AlignmentNotice is null) _lastNotice = null;
    }

    private void ShowToast(string title, string body, bool force = false)
    {
        if (!force && (DateTime.UtcNow - _lastToast).TotalSeconds < 30) return;
        _lastToast = DateTime.UtcNow;
        try
        {
            var toast = new Microsoft.Windows.AppNotifications.Builder.AppNotificationBuilder()
                .AddText(title)
                .AddText(body)
                .BuildNotification();
            Microsoft.Windows.AppNotifications.AppNotificationManager.Default.Show(toast);
        }
        catch { /* toasts are best-effort (headless/remote sessions) */ }
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
