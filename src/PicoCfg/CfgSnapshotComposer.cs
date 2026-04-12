namespace PicoCfg;

internal static class CfgSnapshotComposer
{
    public static ICfgSnapshot CreateSnapshot(IReadOnlyList<ICfgSnapshot> providerSnapshots)
    {
        return providerSnapshots.Count switch
        {
            0 => CfgSnapshot.Empty,
            1 => providerSnapshots[0],
            _ when TryCreateFlattenedSnapshot(providerSnapshots, out var snapshot) => snapshot,
            _ => new CompositeCfgSnapshot(providerSnapshots),
        };
    }

    public static bool SequenceEqual(IReadOnlyList<ICfgSnapshot> left, IReadOnlyList<ICfgSnapshot> right)
    {
        if (left.Count != right.Count)
            return false;

        for (var i = 0; i < left.Count; i++)
        {
            if (!ReferenceEquals(left[i], right[i]))
                return false;
        }

        return true;
    }

    private static bool TryCreateFlattenedSnapshot(
        IReadOnlyList<ICfgSnapshot> providerSnapshots,
        out ICfgSnapshot snapshot
    )
    {
        snapshot = CfgSnapshot.Empty;
        var dictionaries = new IReadOnlyDictionary<string, string>[providerSnapshots.Count];
        var capacity = 0;

        for (var i = 0; i < providerSnapshots.Count; i++)
        {
            if (providerSnapshots[i] is not CfgSnapshot cfgSnapshot)
                return false;

            dictionaries[i] = cfgSnapshot.Values;
            capacity += cfgSnapshot.Values.Count;
        }

        var mergedValues = new Dictionary<string, string>(capacity);
        for (var i = 0; i < dictionaries.Length; i++)
        {
            foreach (var (key, value) in dictionaries[i])
                mergedValues[key] = value;
        }

        snapshot = new CfgSnapshot(mergedValues);
        return true;
    }

    private sealed class CompositeCfgSnapshot(IReadOnlyList<ICfgSnapshot> snapshots) : ICfgSnapshot
    {
        public bool TryGetValue(string path, out string? value)
        {
            for (var i = snapshots.Count - 1; i >= 0; i--)
            {
                if (snapshots[i].TryGetValue(path, out value))
                    return true;
            }

            value = null;
            return false;
        }
    }
}
