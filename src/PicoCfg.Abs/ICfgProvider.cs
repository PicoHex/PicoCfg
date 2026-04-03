namespace PicoCfg.Abs;

/// <summary>
/// Provides configuration snapshots from a single source.
/// Snapshot should always return a stable, concurrently readable snapshot instance.
/// ReloadAsync should replace that snapshot instance when the source values change.
/// WatchAsync returns the current one-shot change signal for this provider.
/// </summary>
public interface ICfgProvider : IAsyncDisposable
{
    ICfgSnapshot Snapshot { get; }
    ValueTask ReloadAsync(CancellationToken ct = default);
    ValueTask<ICfgChangeSignal> WatchAsync(CancellationToken ct = default);
}
