namespace PicoCfg.Abs;

public interface ICfgProvider : IAsyncDisposable
{
    ICfgSnapshot Snapshot { get; }
    ValueTask ReloadAsync(CancellationToken ct = default);
    ValueTask<ICfgChangeSignal> WatchAsync(CancellationToken ct = default);
}
