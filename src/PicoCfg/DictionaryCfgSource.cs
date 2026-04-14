namespace PicoCfg;

internal sealed class DictionaryCfgSource : ICfgSource
{
    private readonly Func<IEnumerable<KeyValuePair<string, string>>> _dataFactory;
    private readonly Func<object?>? _versionStampFactory;

    public DictionaryCfgSource(IDictionary<string, string> configData)
        : this(configData, null)
    {
    }

    public DictionaryCfgSource(IDictionary<string, string> configData, Func<object?>? versionStampFactory)
        : this(() => configData, versionStampFactory)
    {
    }

    public DictionaryCfgSource(Func<IEnumerable<KeyValuePair<string, string>>> dataFactory)
        : this(dataFactory, null)
    {
    }

    public DictionaryCfgSource(
        Func<IEnumerable<KeyValuePair<string, string>>> dataFactory,
        Func<object?>? versionStampFactory
    )
    {
        ArgumentNullException.ThrowIfNull(dataFactory);
        _dataFactory = dataFactory;
        _versionStampFactory = versionStampFactory;
    }

    public async ValueTask<ICfgProvider> OpenAsync(CancellationToken ct = default)
    {
        return await CfgSourceHelpers.OpenAsync(new DictionaryCfgProvider(_dataFactory, _versionStampFactory), ct);
    }
}
