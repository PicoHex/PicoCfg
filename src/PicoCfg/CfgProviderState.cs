namespace PicoCfg;

/// <summary>
/// Tracks the published provider snapshot, its current one-shot change signal, and the optional version stamp
/// used to short-circuit reload work before re-materializing source data.
/// </summary>
internal sealed class CfgProviderState
{
    private readonly Lock _syncRoot = new();
    private bool _hasVersionStamp;
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

    public bool TryBeginReload(
        Func<object?>? versionStampFactory,
        CancellationToken ct,
        out object? versionStamp
    )
    {
        ct.ThrowIfCancellationRequested();
        versionStamp = null;

        if (versionStampFactory is not null)
        {
            versionStamp = versionStampFactory();
            if (IsVersionStampUnchanged(versionStamp))
                return false;
        }

        ct.ThrowIfCancellationRequested();
        return true;
    }

    public bool PublishIfChanged(IReadOnlyDictionary<string, string> values, object? versionStamp)
    {
        var fingerprint = ConfigDataComparer.ComputeFingerprint(values);
        CfgChangeSignal? changedSignal = null;
        lock (_syncRoot)
        {
            _hasVersionStamp = true;
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

    private bool IsVersionStampUnchanged(object? versionStamp)
    {
        lock (_syncRoot)
            return _hasVersionStamp && Equals(_versionStamp, versionStamp);
    }
}
