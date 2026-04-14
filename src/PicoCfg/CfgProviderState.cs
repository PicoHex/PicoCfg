namespace PicoCfg;

/// <summary>
/// Tracks the published provider snapshot, its current one-shot change signal, and the optional version stamp
/// used as the provider's authoritative reload baseline before re-materializing source data.
/// </summary>
internal sealed class CfgProviderState
{
    private readonly Lock _syncRoot = new();
    private bool _hasAcceptedVersionStamp;
    private object? _acceptedVersionStamp;
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
        out object? candidateVersionStamp
    )
    {
        ct.ThrowIfCancellationRequested();
        candidateVersionStamp = null;

        if (versionStampFactory is not null)
        {
            candidateVersionStamp = versionStampFactory();
            if (IsAcceptedVersionStampUnchanged(candidateVersionStamp))
                return false;
        }

        ct.ThrowIfCancellationRequested();
        return true;
    }

    public bool PublishIfChanged(IReadOnlyDictionary<string, string> values, object? candidateVersionStamp)
    {
        var fingerprint = ConfigDataComparer.ComputeFingerprint(values);
        CfgChangeSignal? changedSignal = null;
        lock (_syncRoot)
        {
            // A completed materialization attempt advances the authoritative baseline even when the visible
            // snapshot does not publish, so repeated equal stamps can skip later source work.
            _hasAcceptedVersionStamp = true;
            _acceptedVersionStamp = candidateVersionStamp;

            if (ConfigDataComparer.Equals(_snapshot, values, fingerprint))
                return false;

            _snapshot = new CfgSnapshot(values, fingerprint);
            changedSignal = _changeSignal;
            _changeSignal = new CfgChangeSignal();
        }

        changedSignal.NotifyChanged();
        return true;
    }

    private bool IsAcceptedVersionStampUnchanged(object? candidateVersionStamp)
    {
        lock (_syncRoot)
            return _hasAcceptedVersionStamp && Equals(_acceptedVersionStamp, candidateVersionStamp);
    }
}
