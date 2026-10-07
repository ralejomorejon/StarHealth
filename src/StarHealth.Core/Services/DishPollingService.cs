using StarHealth.Core.Abstractions;
using StarHealth.Core.Models;

namespace StarHealth.Core.Services;

/// <summary>
/// Polls the dish on a fixed cadence and keeps the latest snapshot.
/// The ViewModel subscribes to <see cref="SnapshotUpdated"/>; no UI
/// framework types leak in here so this runs unchanged on Uno targets.
/// </summary>
public sealed class DishPollingService : IAsyncDisposable
{
    private readonly IDishClient _primary;
    private readonly IDishClient _fallback;
    private PeriodicTimer? _timer;

    public DishSnapshot? Latest { get; private set; }
    public bool UsingFallback { get; private set; }
    public string? LastError { get; private set; }

    public event Action<DishSnapshot>? SnapshotUpdated;

    public IDishClient Primary => _primary;

    public Task<ObstructionMapData?> GetObstructionMapAsync(CancellationToken ct = default)
        => _primary.GetObstructionMapAsync(ct);

    public DishPollingService(IDishClient primary, IDishClient fallback)
    {
        _primary = primary;
        _fallback = fallback;
    }

    public async Task StartAsync(TimeSpan interval, CancellationToken ct = default)
    {
        _timer = new PeriodicTimer(interval);
        await PollOnceAsync(ct).ConfigureAwait(false);
        while (await _timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            await PollOnceAsync(ct).ConfigureAwait(false);
    }

    public async Task PollOnceAsync(CancellationToken ct = default)
    {
        try
        {
            Latest = await _primary.GetSnapshotAsync(ct).ConfigureAwait(false);
            UsingFallback = false;
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Latest = await _fallback.GetSnapshotAsync(ct).ConfigureAwait(false);
            UsingFallback = true;
        }
        if (Latest is not null) SnapshotUpdated?.Invoke(Latest);
    }

    public ValueTask DisposeAsync()
    {
        _timer?.Dispose();
        if (_primary is IAsyncDisposable a1) return a1.DisposeAsync();
        if (_primary is IDisposable d1) d1.Dispose();
        return ValueTask.CompletedTask;
    }
}
