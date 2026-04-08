namespace PicoCfg;

using System.Runtime.ExceptionServices;

internal sealed class CfgRoot : ICfgRoot
{
    private readonly Lock _syncRoot = new();
    private readonly SemaphoreSlim _reloadGate = new(1, 1);
    private readonly List<ICfgProvider> _providers;
    private ICfgSnapshot[] _providerSnapshots;
    private ICfgSnapshot _snapshot;
    private CfgChangeSignal _changeSignal = new();

    public CfgRoot(IEnumerable<ICfgProvider> providers)
    {
        _providers = [.. providers];
        _providerSnapshots = [.. _providers.Select(static provider => provider.Snapshot)];
        _snapshot = CreateSnapshot(_providerSnapshots);
    }

    public ICfgSnapshot Snapshot
    {
        get
        {
            lock (_syncRoot)
                return _snapshot;
        }
    }

    public async ValueTask<bool> ReloadAsync(CancellationToken ct = default)
    {
        await _reloadGate.WaitAsync(ct);
        try
        {
            var reloadTasks = new Task<bool>[_providers.Count];
            for (var i = 0; i < _providers.Count; i++)
                reloadTasks[i] = _providers[i].ReloadAsync(ct).AsTask();

            var providerReloadResults = await Task.WhenAll(reloadTasks);

            return PublishSnapshot(providerReloadResults);
        }
        finally
        {
            _reloadGate.Release();
        }
    }

    public ICfgChangeSignal GetChangeSignal()
    {
        lock (_syncRoot)
            return _changeSignal;
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

    private bool PublishSnapshot(bool[] providerReloadResults)
    {
        if (!providerReloadResults.Any(static changed => changed))
            return false;

        var providerSnapshots = (ICfgSnapshot[])_providerSnapshots.Clone();

        for (var i = 0; i < providerReloadResults.Length; i++)
        {
            if (providerReloadResults[i])
                providerSnapshots[i] = _providers[i].Snapshot;
        }

        if (SnapshotSequenceEqual(_providerSnapshots, providerSnapshots))
            return false;

        CfgChangeSignal? changedSignal = null;
        lock (_syncRoot)
        {
            _providerSnapshots = providerSnapshots;
            _snapshot = CreateSnapshot(providerSnapshots);
            changedSignal = _changeSignal;
            _changeSignal = new CfgChangeSignal();
        }

        changedSignal.NotifyChanged();
        return true;
    }

    private static ICfgSnapshot CreateSnapshot(IReadOnlyList<ICfgSnapshot> providerSnapshots)
    {
        return providerSnapshots.Count switch
        {
            0 => CfgSnapshot.Empty,
            1 => providerSnapshots[0],
            _ when TryCreateFlattenedSnapshot(providerSnapshots, out var snapshot) => snapshot,
            _ => new CompositeCfgSnapshot(providerSnapshots),
        };
    }

    private static bool TryCreateFlattenedSnapshot(
        IReadOnlyList<ICfgSnapshot> providerSnapshots,
        out ICfgSnapshot snapshot
    )
    {
        snapshot = CfgSnapshot.Empty;
        var dictionaries = new IReadOnlyDictionary<string, string>[providerSnapshots.Count];
        var capacity = 0;

        for (var i = 0; i < providerSnapshots.Count; i++)
        {
            if (providerSnapshots[i] is not CfgSnapshot cfgSnapshot)
                return false;

            dictionaries[i] = cfgSnapshot.Values;
            capacity += cfgSnapshot.Values.Count;
        }

        var mergedValues = new Dictionary<string, string>(capacity);
        for (var i = 0; i < dictionaries.Length; i++)
        {
            foreach (var (key, value) in dictionaries[i])
                mergedValues[key] = value;
        }

        snapshot = new CfgSnapshot(mergedValues);

        return true;
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
