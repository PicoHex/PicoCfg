namespace PicoCfg.Abs;

/// <summary>
/// Represents an owned configuration runtime that exposes the current published snapshot,
/// supports explicit reload, and can be awaited for the next published change.
/// </summary>
public interface ICfgRuntime : IAsyncDisposable
{
    ICfgSnapshot Current { get; }
    ValueTask<bool> ReloadAsync(CancellationToken ct = default);
    ValueTask WaitForChangeAsync(CancellationToken ct = default);
}
