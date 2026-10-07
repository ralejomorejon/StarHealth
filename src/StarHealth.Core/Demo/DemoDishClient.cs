using StarHealth.Core.Abstractions;
using StarHealth.Core.Localization;
using StarHealth.Core.Models;

namespace StarHealth.Core.Demo;

/// <summary>
/// Realistic offline data source: sine-based throughput + noise, occasional
/// obstructed outages, wedge fractions. Used when the dish is unreachable
/// and for UI development without hardware on the LAN.
/// </summary>
public sealed class DemoDishClient : IDishClient
{
    public string DisplayName => "Demo dish (simulated)";
    public bool IsDemo => true;

    private readonly Random _rng = new(42);
    private readonly List<ThroughputSample> _history = new();
    private readonly List<OutageEvent> _outages = new();
    private DateTimeOffset _boot = DateTimeOffset.UtcNow.AddHours(-7);

    public Task<DishSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var t = (now - _boot).TotalSeconds;

        double wave(double period, double amp, double phase = 0)
            => amp * (0.5 + 0.5 * Math.Sin(t / period * 2 * Math.PI + phase));

        var down = 120 + wave(47, 70) + _rng.NextDouble() * 18;
        var up = 14 + wave(31, 7, 1.2) + _rng.NextDouble() * 3;
        var latency = 28 + wave(23, 12, 2.1) + _rng.NextDouble() * 4;
        var drop = Math.Clamp((_rng.NextDouble() - 0.93) * 0.2, 0, 0.05);
        var obstructed = _rng.NextDouble() < 0.02;

        _history.Add(new ThroughputSample(now, down, up, latency, drop, obstructed, 38));
        while (_history.Count > 900) _history.RemoveAt(0); // 15-min ring at 1/s

        if (obstructed && (_outages.Count == 0 || (now - _outages[^1].Start).TotalSeconds > 45))
            _outages.Add(new OutageEvent(now, TimeSpan.FromSeconds(2 + _rng.NextDouble() * 9),
                OutageCause.Obstructed));
        while (_outages.Count > 30) _outages.RemoveAt(0);

        var wedges = new double[12];
        wedges[3] = 0.18; wedges[4] = 0.11; wedges[9] = 0.04;

        var map = new double[12, 12];
        for (var y = 0; y < 12; y++)
            for (var x = 0; x < 12; x++)
            {
                var dx = x - 5.5; var dy = y - 5.5;
                var r = Math.Sqrt(dx * dx + dy * dy) / 5.5;
                map[y, x] = Math.Clamp(1 - r + (_rng.NextDouble() - 0.5) * 0.15, 0, 1);
            }
        map[0, 2] = 0.05; map[1, 3] = 0.08; // a "tree" in one corner

        var snapshot = new DishSnapshot(
            Timestamp: now,
            FromDemo: true,
            State: obstructed ? DishState.Obstructed : DishState.Connected,
            Device: new DeviceInfo("ut_demo_0001", "mini_demo (Mini, simulado)",
                "demo.firmware.1", "US", "POP-DEMO"),
            Throughput: new ThroughputInfo(down, up, 482_000_000_000UL, 31_000_000_000UL),
            Latency: new LatencyInfo(latency, drop, latency + 14, latency - 3, latency + 22),
            Signal: new SignalInfo(0), // el firmware actual ya no informa SNR por estado
            Alignment: new AlignmentInfo(12.4, 64.8, 11.2, true,
                Text.Get("dish.demo.align", "12,4", "64,8")),
            Obstruction: new ObstructionInfo(0.023, 61_838, 1_422, wedges, map, 8, 640),
            // Simulado: el Mini real puede informar potencia vía get_history.
            Power: new PowerInfo(38, IsEstimate: true, Text.Get("dish.power.est")),
            Location: new LocationInfo(51.5072, -0.1276, 24, 9, true),
            RecentOutages: _outages.OrderByDescending(o => o.Start).Take(10).ToList(),
            ActiveAlerts: obstructed
                ? new[] { new DishAlert("obstructed", Text.Get("dish.demo.obst"), true) }
                : Array.Empty<DishAlert>(),
            History: _history.ToList(),
            Uptime: now - _boot,
            Stats: new HistoryStats(
                Samples: _history.Count,
                LossRatio: _history.Count == 0 ? 0 : _history.Average(p => p.DropRate),
                FullDropSamples: _history.Count(p => p.DropRate >= 1),
                MedianLatencyMs: Median(_history.Select(p => p.LatencyMs)),
                MeanLatencyMs: _history.Count == 0 ? null : _history.Average(p => p.LatencyMs),
                DownloadBytes: 482_000_000_000UL,
                UploadBytes: 31_000_000_000UL,
                LatestPowerW: 38, MeanPowerW: 36.5, MaxPowerW: 44, TotalEnergyKwh: 0.26,
                LongestFullDropRunSec: 4),
            SoftwareUpdate: Text.Get("dish.update.ok"),
            AlignmentNotice: Text.Get("dish.demo.notice"));

        return Task.FromResult(snapshot);
    }

    private static double? Median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        return sorted.Length == 0 ? null : sorted[sorted.Length / 2];
    }

    public Task<ObstructionMapData?> GetObstructionMapAsync(CancellationToken ct = default)
    {
        const int n = 24;
        var snr = new float[n * n];
        var rng = new Random(7);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                double dx = x - (n - 1) / 2.0, dy = y - (n - 1) / 2.0;
                double r = Math.Sqrt(dx * dx + dy * dy) / (n / 2.0);
                double v = Math.Clamp(9 - r * 7 + (rng.NextDouble() - 0.5), 0, 10);
                if (x < 5 && y < 5) v = Math.Min(v, 1.5); // el "árbol" de la demo
                snr[y * n + x] = (float)v;
            }
        return Task.FromResult<ObstructionMapData?>(new ObstructionMapData(n, n, snr, 25));
    }
}
