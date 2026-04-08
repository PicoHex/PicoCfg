namespace PicoCfg.Abs;

/// <summary>
/// Represents the composed configuration root across all registered providers.
/// <see cref="Snapshot"/> must always return a stable, concurrently readable composed snapshot.
/// Snapshot reference identity is the root's published version identity:
/// when <see cref="ReloadAsync"/> returns <see langword="true"/>, <see cref="Snapshot"/> must be a
/// different instance than before the reload; when it returns <see langword="false"/>, the same
/// snapshot instance must be retained.
/// A root publishes a new snapshot when its composed provider snapshot sequence changes.
/// <see cref="GetChangeSignal"/> returns the current one-shot signal for the current published
/// root snapshot version.
/// <see cref="ReloadAsync"/> publishes at most one new composed snapshot per call.
/// When it returns <see langword="false"/>, no provider published a new version and the current
/// <see cref="Snapshot"/> instance is retained.
/// </summary>
public interface ICfgRoot : IAsyncDisposable
{
    ICfgSnapshot Snapshot { get; }
    ValueTask<bool> ReloadAsync(CancellationToken ct = default);
    ICfgChangeSignal GetChangeSignal();
}
