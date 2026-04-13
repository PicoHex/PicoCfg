namespace PicoCfg;

using System.Runtime.ExceptionServices;

internal sealed class CfgRoot : ICfgRoot
{
    private readonly Lock _disposeSyncRoot = new();
    private readonly Lock _syncRoot = new();
    private readonly SemaphoreSlim _reloadGate = new(1, 1);
    private readonly List<ICfgProvider> _providers;
    private ICfgSnapshot[] _providerSnapshots;
    private ICfgSnapshot _snapshot;
    private CfgChangeSignal _changeSignal = new();
    private Task? _disposeTask;

    public CfgRoot(IEnumerable<ICfgProvider> providers)
    {
        _providers = [.. providers];
        _providerSnapshots = [.. _providers.Select(static provider => provider.Snapshot)];
        _snapshot = CfgSnapshotComposer.CreateSnapshot(_providerSnapshots);
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
        Task disposeTask;
        lock (_disposeSyncRoot)
        {
            _disposeTask ??= DisposeCoreAsync();
            disposeTask = _disposeTask;
        }

        await disposeTask;
    }

    private async Task DisposeCoreAsync()
    {
        await _reloadGate.WaitAsync();
        try
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
        finally
        {
            _reloadGate.Release();
        }
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
            _snapshot = CfgSnapshotComposer.CreateSnapshot(providerSnapshots);
            changedSignal = _changeSignal;
            _changeSignal = new CfgChangeSignal();
        }

        changedSignal.NotifyChanged();
        return true;
    }

    private static bool SnapshotSequenceEqual(IReadOnlyList<ICfgSnapshot> left, IReadOnlyList<ICfgSnapshot> right) =>
        CfgSnapshotComposer.SequenceEqual(left, right);
}
