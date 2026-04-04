namespace PicoCfg;

internal sealed class DictionaryCfgProvider : ICfgProvider
{
    private readonly Func<IEnumerable<KeyValuePair<string, string>>> _dataFactory;
    private readonly Lock _syncRoot = new();
    private CfgSnapshot _snapshot = CfgSnapshot.Empty;
    private CfgChangeSignal _changeSignal = new();

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

    public ValueTask<bool> ReloadAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var newData = new Dictionary<string, string>();
        foreach (var (key, value) in _dataFactory())
        {
            ct.ThrowIfCancellationRequested();
            newData[key] = value;
        }

        return ValueTask.FromResult(PublishSnapshot(newData));
    }

    public ICfgChangeSignal GetChangeSignal()
    {
        lock (_syncRoot)
            return _changeSignal;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private bool PublishSnapshot(Dictionary<string, string> newData)
    {
        CfgChangeSignal? changedSignal = null;
        lock (_syncRoot)
        {
            if (ConfigDataComparer.Equals(_snapshot.Values, newData))
                return false;

            _snapshot = new CfgSnapshot(newData);
            changedSignal = _changeSignal;
            _changeSignal = new CfgChangeSignal();
        }

        changedSignal.NotifyChanged();
        return true;
    }
}
