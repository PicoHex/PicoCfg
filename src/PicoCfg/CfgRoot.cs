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
            ExceptionDispatchInfo? reloadFailure = null;
            try
            {
                await ReloadProvidersAsync(ct);
            }
            catch (Exception ex)
            {
                reloadFailure = ExceptionDispatchInfo.Capture(ex);
            }

            // Providers publish their own snapshots first. Re-sample the observed provider sequence after
            // all reload tasks settle so a sibling fault/cancellation does not leave the root behind.
            var observedProviderSnapshots = ObserveProviderSnapshots();
            var changed = TryPublishObservedProviderSnapshots(observedProviderSnapshots);

            reloadFailure?.Throw();
            return changed;
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

    private async Task ReloadProvidersAsync(CancellationToken ct)
    {
        var reloadTasks = new List<Task>(_providers.Count);
        ExceptionDispatchInfo? creationFailure = null;

        for (var i = 0; i < _providers.Count; i++)
        {
            try
            {
                reloadTasks.Add(_providers[i].ReloadAsync(ct).AsTask());
            }
            catch (Exception ex)
            {
                creationFailure = ExceptionDispatchInfo.Capture(ex);
                break;
            }
        }

        try
        {
            await Task.WhenAll(reloadTasks);
        }
        catch when (creationFailure is not null)
        {
            // Preserve the original synchronous creation failure after already-started reloads settle.
        }

        creationFailure?.Throw();
    }

    private ICfgSnapshot[] ObserveProviderSnapshots()
    {
        var observedProviderSnapshots = new ICfgSnapshot[_providers.Count];
        for (var i = 0; i < _providers.Count; i++)
            observedProviderSnapshots[i] = _providers[i].Snapshot;

        return observedProviderSnapshots;
    }

    private bool TryPublishObservedProviderSnapshots(ICfgSnapshot[] observedProviderSnapshots)
    {
        // Root publication is based on provider snapshot identity rather than just the final visible values.
        // A provider can publish a new snapshot that stays overridden by later providers, and callers should
        // still observe a fresh root snapshot/change signal for that publication.
        if (!ProviderSnapshotSequenceChanged(_providerSnapshots, observedProviderSnapshots))
            return false;

        // Compose once on the reload path so steady-state reads stay on the current published snapshot.
        var publishedSnapshot = CfgSnapshotComposer.CreateSnapshot(observedProviderSnapshots);
        return PublishRootSnapshot(observedProviderSnapshots, publishedSnapshot);
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

    private static bool ProviderSnapshotSequenceChanged(
        IReadOnlyList<ICfgSnapshot> currentSnapshots,
        IReadOnlyList<ICfgSnapshot> nextSnapshots
    ) => !CfgSnapshotComposer.SequenceEqual(currentSnapshots, nextSnapshots);
}
