namespace PicoCfg.Abs;

/// <summary>
/// Represents a one-shot signal for a specific published snapshot version.
/// Once <see cref="HasChanged"/> becomes <see langword="true"/>, the signal remains changed and
/// callers should obtain a new signal instance from the owning provider or root for subsequent waits.
/// </summary>
public interface ICfgChangeSignal
{
    bool HasChanged { get; }
    ValueTask WaitForChangeAsync(CancellationToken ct = default);
}
