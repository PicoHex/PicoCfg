namespace PicoCfg;

internal static class ConfigDataComparer
{
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
}
