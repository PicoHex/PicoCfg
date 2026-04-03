namespace PicoCfg.Abs;

/// <summary>
/// Represents an immutable configuration snapshot that is safe to read concurrently.
/// Providers should replace the snapshot instance when the underlying values change.
/// </summary>
public interface ICfgSnapshot
{
    bool TryGetValue(string path, out string? value);
}
