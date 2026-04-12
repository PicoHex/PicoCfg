namespace PicoCfg;

internal sealed class DictionaryCfgProvider : ICfgProvider
{
    private readonly Func<IEnumerable<KeyValuePair<string, string>>> _dataFactory;
    private readonly Func<object?>? _versionStampFactory;
    private readonly Lock _syncRoot = new();
    private object? _versionStamp;
    private CfgSnapshot _snapshot = CfgSnapshot.Empty;
    private CfgChangeSignal _changeSignal = new();

    public DictionaryCfgProvider(Func<IEnumerable<KeyValuePair<string, string>>> dataFactory)
        : this(dataFactory, null)
    {
    }

    public DictionaryCfgProvider(
        Func<IEnumerable<KeyValuePair<string, string>>> dataFactory,
        Func<object?>? versionStampFactory
    )
    {
        ArgumentNullException.ThrowIfNull(dataFactory);
        _dataFactory = dataFactory;
        _versionStampFactory = versionStampFactory;
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

        var versionStampFactory = _versionStampFactory;
        var versionStamp = versionStampFactory?.Invoke();
        if (versionStampFactory is not null)
        {
            lock (_syncRoot)
            {
                if (Equals(_versionStamp, versionStamp))
                    return ValueTask.FromResult(false);
            }
        }

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

        var fingerprint = ConfigDataComparer.ComputeFingerprint(newData);
        return ValueTask.FromResult(PublishSnapshot(newData, fingerprint, versionStamp));
    }

    public ICfgChangeSignal GetChangeSignal()
    {
        lock (_syncRoot)
            return _changeSignal;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private bool PublishSnapshot(Dictionary<string, string> newData, ulong fingerprint, object? versionStamp)
    {
        CfgChangeSignal? changedSignal = null;
        lock (_syncRoot)
        {
            _versionStamp = versionStamp;

            if (ConfigDataComparer.Equals(_snapshot, newData, fingerprint))
                return false;

            _snapshot = new CfgSnapshot(newData, fingerprint);
            changedSignal = _changeSignal;
            _changeSignal = new CfgChangeSignal();
        }

        changedSignal.NotifyChanged();
        return true;
    }
}
