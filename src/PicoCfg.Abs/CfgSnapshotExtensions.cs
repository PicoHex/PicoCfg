namespace PicoCfg.Abs;

public static class CfgSnapshotExtensions
{
    /// <summary>
    /// Gets the exact string key from the snapshot, or <see langword="null"/> when the key is absent.
    /// This is an exact lookup over the provided key string; the API does not interpret separators such as
    /// <c>:</c> or <c>.</c> as hierarchical traversal.
    /// </summary>
    public static string? GetValue(this ICfgSnapshot snapshot, string path)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return snapshot.TryGetValue(path, out var value) ? value : null;
    }
}
