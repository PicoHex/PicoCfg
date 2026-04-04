namespace PicoCfg.Abs;

/// <summary>
/// Provides configuration snapshots from a single source.
/// <see cref="Snapshot"/> must always return a stable, concurrently readable snapshot instance.
/// Snapshot reference identity is the provider's published version identity:
/// when <see cref="ReloadAsync"/> returns <see langword="true"/>, <see cref="Snapshot"/> must be a
/// different instance than before the reload; when it returns <see langword="false"/>, the same
/// snapshot instance must be retained.
/// <see cref="GetChangeSignal"/> returns the current one-shot signal for the current published
/// snapshot version.
/// </summary>
public interface ICfgProvider : IAsyncDisposable
{
    ICfgSnapshot Snapshot { get; }
    ValueTask<bool> ReloadAsync(CancellationToken ct = default);
    ICfgChangeSignal GetChangeSignal();
}
