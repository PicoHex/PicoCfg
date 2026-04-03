namespace PicoCfg;

internal sealed class CfgSnapshot(IReadOnlyDictionary<string, string> values) : ICfgSnapshot
{
    public static CfgSnapshot Empty { get; } = new(new Dictionary<string, string>());

    internal IReadOnlyDictionary<string, string> Values { get; } = values;

    public bool TryGetValue(string path, out string? value)
    {
        if (Values.TryGetValue(path, out var existingValue))
        {
            value = existingValue;
            return true;
        }

        value = null;
        return false;
    }
}
