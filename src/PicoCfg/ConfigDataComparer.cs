namespace PicoCfg;

internal static class ConfigDataComparer
{
    public static ulong ComputeFingerprint(IEnumerable<KeyValuePair<string, string>> values)
    {
        const ulong seed = 14695981039346656037UL;
        var fingerprint = seed;

        foreach (var (key, value) in values)
        {
            fingerprint += Mix(key, value);
        }

        return fingerprint;
    }

    public static bool Equals(CfgSnapshot left, IReadOnlyDictionary<string, string> right, ulong rightFingerprint)
    {
        if (left.Values.Count != right.Count)
            return false;

        if (left.Fingerprint != rightFingerprint)
            return false;

        return Equals(left.Values, right);
    }

    public static bool Equals(
        IReadOnlyDictionary<string, string> left,
        IReadOnlyDictionary<string, string> right
    )
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left.Count != right.Count)
            return false;

        foreach (var (key, value) in left)
        {
            if (!right.TryGetValue(key, out var otherValue))
                return false;

            if (!string.Equals(value, otherValue, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static ulong Mix(string key, string value)
    {
        var hash = new HashCode();
        hash.Add(key, StringComparer.Ordinal);
        hash.Add(value, StringComparer.Ordinal);
        return unchecked((ulong)hash.ToHashCode());
    }
}
