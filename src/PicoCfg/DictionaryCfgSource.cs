namespace PicoCfg;

internal sealed class DictionaryCfgSource : ICfgSource
{
    private readonly Func<IEnumerable<KeyValuePair<string, string>>> _dataFactory;
    private readonly Func<object?>? _versionStampFactory;
    private readonly Func<CfgProviderState> _providerStateFactory;

    public DictionaryCfgSource(IDictionary<string, string> configData)
        : this(() => configData, null, CfgBuilder.CreateDefaultProviderState)
    {
    }

    public DictionaryCfgSource(IDictionary<string, string> configData, Func<object?>? versionStampFactory)
        : this(() => configData, versionStampFactory, CfgBuilder.CreateDefaultProviderState)
    {
    }

    public DictionaryCfgSource(Func<IEnumerable<KeyValuePair<string, string>>> dataFactory)
        : this(dataFactory, null, CfgBuilder.CreateDefaultProviderState)
    {
    }

    public DictionaryCfgSource(
        Func<IEnumerable<KeyValuePair<string, string>>> dataFactory,
        Func<object?>? versionStampFactory
    )
        : this(dataFactory, versionStampFactory, CfgBuilder.CreateDefaultProviderState)
    {
    }

    internal DictionaryCfgSource(
        Func<IEnumerable<KeyValuePair<string, string>>> dataFactory,
        Func<object?>? versionStampFactory,
        Func<CfgProviderState> providerStateFactory
    )
    {
        ArgumentNullException.ThrowIfNull(dataFactory);
        ArgumentNullException.ThrowIfNull(providerStateFactory);
        _dataFactory = dataFactory;
        _versionStampFactory = versionStampFactory;
        _providerStateFactory = providerStateFactory;
    }

    public async ValueTask<ICfgProvider> OpenAsync(CancellationToken ct = default)
    {
        return await CfgSourceHelpers.OpenAsync(
            new DictionaryCfgProvider(_dataFactory, _versionStampFactory, _providerStateFactory),
            ct
        );
    }
}
