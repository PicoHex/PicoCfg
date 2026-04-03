namespace PicoCfg;

public class DictionaryCfgSource : ICfgSource
{
    private readonly Func<IEnumerable<KeyValuePair<string, string>>> _dataFactory;

    public DictionaryCfgSource(IDictionary<string, string> configData)
        : this(() => configData)
    {
    }

    public DictionaryCfgSource(Func<IEnumerable<KeyValuePair<string, string>>> dataFactory)
    {
        ArgumentNullException.ThrowIfNull(dataFactory);
        _dataFactory = dataFactory;
    }

    public async ValueTask<ICfgProvider> OpenAsync(CancellationToken ct = default)
    {
        var provider = new DictionaryCfgProvider(_dataFactory);
        await provider.ReloadAsync(ct);
        return provider;
    }
}
