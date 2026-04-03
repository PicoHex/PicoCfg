namespace PicoCfg.Abs;

public static class CfgSnapshotExtensions
{
    public static string? GetValue(this ICfgSnapshot snapshot, string path)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return snapshot.TryGetValue(path, out var value) ? value : null;
    }
}
