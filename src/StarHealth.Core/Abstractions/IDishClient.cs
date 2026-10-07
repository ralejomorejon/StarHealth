using StarHealth.Core.Models;

namespace StarHealth.Core.Abstractions;

/// <summary>
/// UI depends only on this. Implementations: gRPC (live) and demo (offline).
/// Keeps WinUI/Uno heads trivially swappable and testable.
/// </summary>
public interface IDishClient
{
    string DisplayName { get; }
    bool IsDemo { get; }
    Task<DishSnapshot> GetSnapshotAsync(CancellationToken ct = default);

    /// <summary>
    /// Obstruction heatmap. Default is "unsupported" so existing
    /// implementations (and the demo until it opts in) keep compiling.
    /// </summary>
    Task<ObstructionMapData?> GetObstructionMapAsync(CancellationToken ct = default)
        => Task.FromResult<ObstructionMapData?>(null);
}
