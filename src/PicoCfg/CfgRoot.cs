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
            var providerReloadResults = await ReloadProvidersAsync(ct);
            return TryPublishReloadedSnapshot(providerReloadResults);
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

    private async Task<bool[]> ReloadProvidersAsync(CancellationToken ct)
    {
        var reloadTasks = new Task<bool>[_providers.Count];
        for (var i = 0; i < _providers.Count; i++)
            reloadTasks[i] = _providers[i].ReloadAsync(ct).AsTask();

        return await Task.WhenAll(reloadTasks);
    }

    private bool TryPublishReloadedSnapshot(bool[] providerReloadResults)
    {
        if (!AnyProviderReloaded(providerReloadResults))
            return false;

        var reloadedProviderSnapshots = CreateReloadedProviderSnapshots(providerReloadResults);

        // Root publication is based on provider snapshot identity rather than just the final visible values.
        // A provider can publish a new snapshot that stays overridden by later providers, and callers should
        // still observe a fresh root snapshot/change signal for that publication.
        if (!ProviderSnapshotSequenceChanged(_providerSnapshots, reloadedProviderSnapshots))
            return false;

        // Compose once on the reload path so steady-state reads stay on the current published snapshot.
        var publishedSnapshot = CfgSnapshotComposer.CreateSnapshot(reloadedProviderSnapshots);
        return PublishRootSnapshot(reloadedProviderSnapshots, publishedSnapshot);
    }

    private ICfgSnapshot[] CreateReloadedProviderSnapshots(IReadOnlyList<bool> providerReloadResults)
    {
        var reloadedProviderSnapshots = (ICfgSnapshot[])_providerSnapshots.Clone();

        for (var i = 0; i < providerReloadResults.Count; i++)
        {
            if (providerReloadResults[i])
                reloadedProviderSnapshots[i] = _providers[i].Snapshot;
        }

        return reloadedProviderSnapshots;
    }

    private bool PublishRootSnapshot(ICfgSnapshot[] providerSnapshots, ICfgSnapshot snapshot)
    {
        CfgChangeSignal? changedSignal = null;
        lock (_syncRoot)
        {
            _providerSnapshots = providerSnapshots;
            _snapshot = snapshot;
            changedSignal = _changeSignal;
            _changeSignal = new CfgChangeSignal();
        }

        changedSignal.NotifyChanged();
        return true;
    }

    private static bool AnyProviderReloaded(IReadOnlyList<bool> providerReloadResults)
    {
        for (var i = 0; i < providerReloadResults.Count; i++)
        {
            if (providerReloadResults[i])
                return true;
        }

        return false;
    }

    private static bool ProviderSnapshotSequenceChanged(
        IReadOnlyList<ICfgSnapshot> currentSnapshots,
        IReadOnlyList<ICfgSnapshot> nextSnapshots
    ) => !CfgSnapshotComposer.SequenceEqual(currentSnapshots, nextSnapshots);
}
