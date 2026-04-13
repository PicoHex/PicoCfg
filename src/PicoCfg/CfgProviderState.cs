namespace PicoCfg;

/// <summary>
/// Tracks the published provider snapshot, its current one-shot change signal, and the optional version stamp
/// used to short-circuit reload work before re-materializing source data.
/// </summary>
internal sealed class CfgProviderState
{
    private readonly Lock _syncRoot = new();
    private object? _versionStamp;
    private CfgSnapshot _snapshot = CfgSnapshot.Empty;
    private CfgChangeSignal _changeSignal = new();

    public ICfgSnapshot Snapshot
    {
        get
        {
            lock (_syncRoot)
                return _snapshot;
        }
    }

    public ICfgChangeSignal GetChangeSignal()
    {
        lock (_syncRoot)
            return _changeSignal;
    }

    public bool IsVersionStampUnchanged(object? versionStamp)
    {
        lock (_syncRoot)
            return Equals(_versionStamp, versionStamp);
    }

    public bool PublishIfChanged(
        IReadOnlyDictionary<string, string> values,
        ulong fingerprint,
        object? versionStamp
    )
    {
        CfgChangeSignal? changedSignal = null;
        lock (_syncRoot)
        {
            _versionStamp = versionStamp;

            if (ConfigDataComparer.Equals(_snapshot, values, fingerprint))
                return false;

            _snapshot = new CfgSnapshot(values, fingerprint);
            changedSignal = _changeSignal;
            _changeSignal = new CfgChangeSignal();
        }

        changedSignal.NotifyChanged();
        return true;
    }
}
