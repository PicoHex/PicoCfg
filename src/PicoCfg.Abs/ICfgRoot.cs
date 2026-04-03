namespace PicoCfg.Abs;

/// <summary>
/// Represents the composed configuration root across all registered providers.
/// Snapshot changes are published when ReloadAsync refreshes provider snapshots and the
/// composed view changes. WatchAsync returns the current one-shot change signal.
/// </summary>
public interface ICfgRoot : IAsyncDisposable
{
    ICfgSnapshot Snapshot { get; }
    ValueTask ReloadAsync(CancellationToken ct = default);
    ValueTask<ICfgChangeSignal> WatchAsync(CancellationToken ct = default);
}
