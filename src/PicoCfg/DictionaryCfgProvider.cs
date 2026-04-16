namespace PicoCfg;

internal sealed class DictionaryCfgProvider : ICfgProvider
{
    private readonly Func<IEnumerable<KeyValuePair<string, string>>> _dataFactory;
    private readonly Func<object?>? _versionStampFactory;
    private readonly CfgProviderState _state;

    public DictionaryCfgProvider(Func<IEnumerable<KeyValuePair<string, string>>> dataFactory)
        : this(dataFactory, null, CfgBuilder.CreateDefaultProviderState)
    {
    }

    public DictionaryCfgProvider(
        Func<IEnumerable<KeyValuePair<string, string>>> dataFactory,
        Func<object?>? versionStampFactory
    )
        : this(dataFactory, versionStampFactory, CfgBuilder.CreateDefaultProviderState)
    {
    }

    internal DictionaryCfgProvider(
        Func<IEnumerable<KeyValuePair<string, string>>> dataFactory,
        Func<object?>? versionStampFactory,
        Func<CfgProviderState> providerStateFactory
    )
    {
        ArgumentNullException.ThrowIfNull(dataFactory);
        ArgumentNullException.ThrowIfNull(providerStateFactory);
        _dataFactory = dataFactory;
        _versionStampFactory = versionStampFactory;
        _state = providerStateFactory()
            ?? throw new InvalidOperationException("The provider state factory returned null.");
    }

    public ICfgSnapshot Snapshot => _state.Snapshot;

    public ValueTask<bool> ReloadAsync(CancellationToken ct = default)
    {
        if (!_state.TryBeginReload(_versionStampFactory, ct, out var candidateVersionStamp))
            return ValueTask.FromResult(false);

        var newData = CreateSnapshotData(ct);
        return ValueTask.FromResult(_state.PublishIfChanged(newData, candidateVersionStamp));
    }

    public ICfgChangeSignal GetChangeSignal() => _state.GetChangeSignal();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private Dictionary<string, string> CreateSnapshotData(CancellationToken ct)
    {
        var sourceData = _dataFactory();
        var newData = sourceData is ICollection<KeyValuePair<string, string>> collection
            ? new Dictionary<string, string>(collection.Count)
            : sourceData.TryGetNonEnumeratedCount(out var count)
                ? new Dictionary<string, string>(count)
                : new Dictionary<string, string>();

        foreach (var (key, value) in sourceData)
        {
            ct.ThrowIfCancellationRequested();
            newData[key] = value;
        }

        return newData;
    }
}
