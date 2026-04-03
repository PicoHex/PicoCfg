namespace PicoCfg;

public class DictionaryCfgProvider : ICfgProvider
{
    private readonly Func<IEnumerable<KeyValuePair<string, string>>> _dataFactory;
    private readonly Lock _syncRoot = new();
    private CfgSnapshot _snapshot = CfgSnapshot.Empty;
    private StreamChangeToken _changeToken = new();

    public DictionaryCfgProvider(IDictionary<string, string> configData)
        : this(() => configData)
    {
    }

    public DictionaryCfgProvider(Func<IEnumerable<KeyValuePair<string, string>>> dataFactory)
    {
        ArgumentNullException.ThrowIfNull(dataFactory);
        _dataFactory = dataFactory;
    }

    public ICfgSnapshot Snapshot
    {
        get
        {
            lock (_syncRoot)
                return _snapshot;
        }
    }

    public ValueTask ReloadAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var newData = new Dictionary<string, string>();
        foreach (var (key, value) in _dataFactory())
        {
            ct.ThrowIfCancellationRequested();
            newData[key] = value;
        }

        PublishSnapshot(newData);
        return ValueTask.CompletedTask;
    }

    public ValueTask<ICfgChangeSignal> WatchAsync(CancellationToken ct = default)
    {
        lock (_syncRoot)
            return ValueTask.FromResult<ICfgChangeSignal>(_changeToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private void PublishSnapshot(Dictionary<string, string> newData)
    {
        StreamChangeToken? changedToken = null;
        lock (_syncRoot)
        {
            if (ConfigDataComparer.Equals(_snapshot.Values, newData))
                return;

            _snapshot = new CfgSnapshot(newData);
            changedToken = _changeToken;
            _changeToken = new StreamChangeToken();
        }

        changedToken.NotifyChanged();
    }
}
