namespace PicoCfg.Abs;

public interface ICfgRoot : IAsyncDisposable
{
    ICfgSnapshot Snapshot { get; }
    ValueTask ReloadAsync(CancellationToken ct = default);
    ValueTask<ICfgChangeSignal> WatchAsync(CancellationToken ct = default);
}
