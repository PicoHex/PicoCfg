namespace PicoCfg;

using System.Runtime.ExceptionServices;

internal sealed class CfgRoot : ICfgRoot
{
    private readonly Lock _syncRoot = new();
    private readonly List<ICfgProvider> _providers;
    private ICfgSnapshot[] _providerSnapshots;
    private ICfgSnapshot _snapshot;
    private StreamChangeToken _changeToken = new();

    public CfgRoot(IEnumerable<ICfgProvider> providers)
    {
        _providers = [.. providers];
        _providerSnapshots = _providers.Select(static provider => provider.Snapshot).ToArray();
        _snapshot = new CompositeCfgSnapshot(_providerSnapshots);
    }

    public ICfgSnapshot Snapshot
    {
        get
        {
            lock (_syncRoot)
                return _snapshot;
        }
    }

    public async ValueTask ReloadAsync(CancellationToken ct = default)
    {
        foreach (var provider in _providers)
            await provider.ReloadAsync(ct);

        PublishSnapshot();
    }

    public ValueTask<ICfgChangeSignal> WatchAsync(CancellationToken ct = default)
    {
        lock (_syncRoot)
            return ValueTask.FromResult<ICfgChangeSignal>(_changeToken);
    }

    public async ValueTask DisposeAsync()
    {
        List<Exception>? exceptions = null;

        for (var i = _providers.Count - 1; i >= 0; i--)
        {
            try
            {
                await _providers[i].DisposeAsync();
            }
            catch (Exception ex)
            {
                exceptions ??= [];
                exceptions.Add(ex);
            }
        }

        if (exceptions is null)
            return;

        if (exceptions.Count is 1)
            ExceptionDispatchInfo.Throw(exceptions[0]);

        throw new AggregateException(exceptions);
    }

    private void PublishSnapshot()
    {
        var providerSnapshots = _providers.Select(static provider => provider.Snapshot).ToArray();

        StreamChangeToken? changedToken = null;
        lock (_syncRoot)
        {
            if (SnapshotSequenceEqual(_providerSnapshots, providerSnapshots))
                return;

            _providerSnapshots = providerSnapshots;
            _snapshot = new CompositeCfgSnapshot(providerSnapshots);
            changedToken = _changeToken;
            _changeToken = new StreamChangeToken();
        }

        changedToken.NotifyChanged();
    }

    private static bool SnapshotSequenceEqual(IReadOnlyList<ICfgSnapshot> left, IReadOnlyList<ICfgSnapshot> right)
    {
        if (left.Count != right.Count)
            return false;

        for (var i = 0; i < left.Count; i++)
        {
            if (!ReferenceEquals(left[i], right[i]))
                return false;
        }

        return true;
    }

    private sealed class CompositeCfgSnapshot(IReadOnlyList<ICfgSnapshot> snapshots) : ICfgSnapshot
    {
        public bool TryGetValue(string path, out string? value)
        {
            for (var i = snapshots.Count - 1; i >= 0; i--)
            {
                if (snapshots[i].TryGetValue(path, out value))
                    return true;
            }

            value = null;
            return false;
        }
    }
}
