using Microsoft.Data.Sqlite;
using StarHealth.Core.Localization;
using StarHealth.Core.Models;

namespace StarHealth.Data.Store;

public sealed record SampleRow(long Ts, double Down, double Up, double Lat, double Drop, double? Power);
public sealed record StoredOutage(long Start, double DurS, string Cause);
public sealed record Availability(double Ratio, int Samples);

/// <summary>
/// Local history: every poll lands here, so charts, availability and reports
/// survive restarts. The dish only keeps ~15 minutes; this keeps 30 days of
/// per-poll samples plus the (small) outage/event tables.
/// Timestamps are Unix milliseconds. Event/outage display text is stored as
/// shown at record time (see note on <see cref="RecordAsync"/>).
/// </summary>
public sealed class HistoryStore
{
    private const int SampleRetentionDays = 30;
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _ready;

    public HistoryStore(string dbPath) => _path = dbPath;

    private async Task EnsureAsync(SqliteConnection? shared = null)
    {
        if (_ready) return;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_ready) return;
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var cs = new SqliteConnectionStringBuilder { DataSource = _path }.ToString();
            await using var c = shared ?? new SqliteConnection(cs);
            if (shared is null) await c.OpenAsync().ConfigureAwait(false);
            foreach (var ddl in new[]
            {
                @"CREATE TABLE IF NOT EXISTS samples(
                    ts INTEGER NOT NULL, dish TEXT NOT NULL,
                    down REAL NOT NULL, up REAL NOT NULL, lat REAL NOT NULL,
                    loss REAL NOT NULL, power REAL, obst INTEGER NOT NULL, state INTEGER NOT NULL)",
                "CREATE INDEX IF NOT EXISTS ix_samples ON samples(dish, ts)",
                @"CREATE TABLE IF NOT EXISTS outages(
                    dish TEXT NOT NULL, start INTEGER NOT NULL,
                    dur_s REAL NOT NULL, cause TEXT NOT NULL,
                    PRIMARY KEY(dish, start, cause)) WITHOUT ROWID",
                @"CREATE TABLE IF NOT EXISTS events(
                    dish TEXT NOT NULL, ts INTEGER NOT NULL,
                    dur_s REAL NOT NULL, severity TEXT NOT NULL, reason TEXT NOT NULL,
                    PRIMARY KEY(dish, ts, reason)) WITHOUT ROWID",
            })
            {
                await using var cmd = c.CreateCommand();
                cmd.CommandText = ddl;
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
            // Retention: samples are bulky, outages/events are tiny and kept.
            await using (var prune = c.CreateCommand())
            {
                prune.CommandText = "DELETE FROM samples WHERE ts < $cut";
                prune.Parameters.AddWithValue("$cut",
                    DateTimeOffset.UtcNow.AddDays(-SampleRetentionDays).ToUnixTimeMilliseconds());
                await prune.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
            _ready = true;
        }
        finally { _gate.Release(); }
    }

    private async Task<SqliteConnection> OpenAsync()
    {
        await EnsureAsync().ConfigureAwait(false);
        var c = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = _path }.ToString());
        await c.OpenAsync().ConfigureAwait(false);
        return c;
    }

    public async Task RecordAsync(string dish, DishSnapshot s)
    {
        try
        {
            await using var c = await OpenAsync().ConfigureAwait(false);
            long now = s.Timestamp.ToUnixTimeMilliseconds();
            bool obst = s.History.Count > 0 && s.History[^1].Obstructed;
            await using (var ins = c.CreateCommand())
            {
                ins.CommandText = @"INSERT INTO samples
                    (ts, dish, down, up, lat, loss, power, obst, state)
                    VALUES ($ts, $dish, $down, $up, $lat, $loss, $power, $obst, $state)";
                ins.Parameters.AddWithValue("$ts", now);
                ins.Parameters.AddWithValue("$dish", dish);
                ins.Parameters.AddWithValue("$down", s.Throughput.DownMbps);
                ins.Parameters.AddWithValue("$up", s.Throughput.UpMbps);
                ins.Parameters.AddWithValue("$lat", s.Latency.PopPingMs);
                ins.Parameters.AddWithValue("$loss", s.Latency.DropRate);
                ins.Parameters.AddWithValue("$power",
                    s.Power.Watts is double w ? w : DBNull.Value);
                ins.Parameters.AddWithValue("$obst", obst ? 1 : 0);
                ins.Parameters.AddWithValue("$state", (int)s.State);
                await ins.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
            foreach (var o in s.RecentOutages)
                await UpsertOutageAsync(c, dish, o).ConfigureAwait(false);
            foreach (var e in s.RecentEvents ?? Enumerable.Empty<DishEvent>())
                await UpsertEventAsync(c, dish, e).ConfigureAwait(false);
        }
        catch { /* history must never break the live dashboard */ }
    }

    private static async Task UpsertOutageAsync(SqliteConnection c, string dish, OutageEvent o)
    {
        await using var cmd = c.CreateCommand();
        cmd.CommandText = @"INSERT OR IGNORE INTO outages (dish, start, dur_s, cause)
            VALUES ($dish, $start, $dur, $cause)";
        cmd.Parameters.AddWithValue("$dish", dish);
        cmd.Parameters.AddWithValue("$start", o.Start.ToUnixTimeMilliseconds());
        cmd.Parameters.AddWithValue("$dur", o.Duration.TotalSeconds);
        cmd.Parameters.AddWithValue("$cause", o.Cause.ToString());
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task UpsertEventAsync(SqliteConnection c, string dish, DishEvent e)
    {
        await using var cmd = c.CreateCommand();
        cmd.CommandText = @"INSERT OR IGNORE INTO events (dish, ts, dur_s, severity, reason)
            VALUES ($dish, $ts, $dur, $sev, $reason)";
        cmd.Parameters.AddWithValue("$dish", dish);
        cmd.Parameters.AddWithValue("$ts", e.Timestamp.ToUnixTimeMilliseconds());
        cmd.Parameters.AddWithValue("$dur", e.Duration.TotalSeconds);
        cmd.Parameters.AddWithValue("$sev", e.Severity);
        cmd.Parameters.AddWithValue("$reason", e.Reason);
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task<List<SampleRow>> GetSeriesAsync(string dish, long fromMs, int maxPoints = 1200)
    {
        await using var c = await OpenAsync().ConfigureAwait(false);
        await using var cmd = c.CreateCommand();
        cmd.CommandText = @"SELECT ts, down, up, lat, loss, power FROM samples
            WHERE dish = $dish AND ts >= $from ORDER BY ts";
        cmd.Parameters.AddWithValue("$dish", dish);
        cmd.Parameters.AddWithValue("$from", fromMs);
        var rows = new List<SampleRow>();
        await using var r = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await r.ReadAsync().ConfigureAwait(false))
            rows.Add(new SampleRow(r.GetInt64(0), r.GetDouble(1), r.GetDouble(2),
                r.GetDouble(3), r.GetDouble(4), r.IsDBNull(5) ? null : r.GetDouble(5)));
        if (rows.Count <= maxPoints) return rows;
        int stride = (rows.Count + maxPoints - 1) / maxPoints;
        return rows.Where((_, i) => i % stride == 0).ToList();
    }

    public async Task<Availability> GetAvailabilityAsync(string dish, long fromMs)
    {
        await using var c = await OpenAsync().ConfigureAwait(false);
        await using var cmd = c.CreateCommand();
        cmd.CommandText = @"SELECT COUNT(*), SUM(CASE WHEN loss < 1 THEN 1 ELSE 0 END)
            FROM samples WHERE dish = $dish AND ts >= $from";
        cmd.Parameters.AddWithValue("$dish", dish);
        cmd.Parameters.AddWithValue("$from", fromMs);
        await using var r = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        if (!await r.ReadAsync().ConfigureAwait(false) || r.IsDBNull(0)) return new(1, 0);
        int total = r.GetInt32(0);
        int ok = r.IsDBNull(1) ? 0 : Convert.ToInt32(r.GetInt64(1));
        return new(total == 0 ? 1 : (double)ok / total, total);
    }

    public async Task<List<StoredOutage>> GetOutagesAsync(string dish, long fromMs, int limit = 200)
    {
        await using var c = await OpenAsync().ConfigureAwait(false);
        await using var cmd = c.CreateCommand();
        cmd.CommandText = @"SELECT start, dur_s, cause FROM outages
            WHERE dish = $dish AND start >= $from ORDER BY start DESC LIMIT $lim";
        cmd.Parameters.AddWithValue("$dish", dish);
        cmd.Parameters.AddWithValue("$from", fromMs);
        cmd.Parameters.AddWithValue("$lim", limit);
        var out_ = new List<StoredOutage>();
        await using var r = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await r.ReadAsync().ConfigureAwait(false))
            out_.Add(new StoredOutage(r.GetInt64(0), r.GetDouble(1), r.GetString(2)));
        return out_;
    }

    public async Task<(double AvgW, double MaxW)> GetPowerAsync(string dish, long fromMs)
    {
        await using var c = await OpenAsync().ConfigureAwait(false);
        await using var cmd = c.CreateCommand();
        cmd.CommandText = @"SELECT AVG(power), MAX(power) FROM samples
            WHERE dish = $dish AND ts >= $from AND power IS NOT NULL";
        cmd.Parameters.AddWithValue("$dish", dish);
        cmd.Parameters.AddWithValue("$from", fromMs);
        await using var r = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        if (!await r.ReadAsync().ConfigureAwait(false) || r.IsDBNull(0)) return (0, 0);
        return (r.GetDouble(0), r.IsDBNull(1) ? 0 : r.GetDouble(1));
    }

    /// <summary>Plain-text outage report for support tickets and records.</summary>
    public async Task<string> BuildReportAsync(string dish, string rangeLabel, long fromMs)
    {
        var now = DateTimeOffset.Now;
        var avail = await GetAvailabilityAsync(dish, fromMs).ConfigureAwait(false);
        var outages = await GetOutagesAsync(dish, fromMs).ConfigureAwait(false);
        var (avgW, maxW) = await GetPowerAsync(dish, fromMs).ConfigureAwait(false);
        double totalS = outages.Sum(o => o.DurS);
        double longest = outages.Count == 0 ? 0 : outages.Max(o => o.DurS);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(Text.Get("rpt.title"));
        sb.AppendLine($"{dish} · {rangeLabel} · {now:yyyy-MM-dd HH:mm}");
        sb.AppendLine(Text.Get("rpt.avail", $"{avail.Ratio * 100:F2}%", avail.Samples));
        if (avgW > 0) sb.AppendLine(Text.Get("rpt.energy", $"{avgW:F0}", $"{maxW:F0}"));
        sb.AppendLine(Text.Get("rpt.outages", outages.Count, $"{totalS:F0}", $"{longest:F1}"));
        foreach (var o in outages)
        {
            var t = DateTimeOffset.FromUnixTimeMilliseconds(o.Start).ToLocalTime();
            sb.AppendLine($"- {t:yyyy-MM-dd HH:mm:ss} · {o.DurS:F1} s · {Text.CauseName(o.Cause)}");
        }
        if (outages.Count == 0) sb.AppendLine(Text.Get("misc.out.none"));
        return sb.ToString();
    }
}
