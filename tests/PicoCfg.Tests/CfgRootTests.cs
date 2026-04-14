namespace PicoCfg.Tests;

public class CfgRootTests
{
    [Test]
    public async Task Snapshot_WithNoProviders_ReturnsMissingValues()
    {
        var root = new CfgRoot([]);

        await Assert.That(root.Snapshot.GetValue("key")).IsNull();
    }

    [Test]
    public async Task Snapshot_WithSingleProvider_ReturnsProviderValue()
    {
        var provider = new MockProvider([new Dictionary<string, string> { ["key"] = "value" }]);
        var root = new CfgRoot([provider]);

        await Assert.That(root.Snapshot.GetValue("key")).IsEqualTo("value");
    }

    [Test]
    public async Task Snapshot_WithMultipleProviders_UsesLastProviderValue()
    {
        var provider1 = new MockProvider([new Dictionary<string, string> { ["key"] = "first" }]);
        var provider2 = new MockProvider([new Dictionary<string, string> { ["key"] = "second" }]);
        var root = new CfgRoot([provider1, provider2]);

        await Assert.That(root.Snapshot.GetValue("key")).IsEqualTo("second");
    }

    [Test]
    public async Task ReloadAsync_RefreshesSnapshotFromProviders()
    {
        var provider = new MockProvider(
            [
                new Dictionary<string, string> { ["key"] = "before" },
                new Dictionary<string, string> { ["key"] = "after" },
            ]
        );
        var root = new CfgRoot([provider]);
        var originalSnapshot = root.Snapshot;

        var changed = await root.ReloadAsync();

        await Assert.That(provider.ReloadCount).IsEqualTo(1);
        await Assert.That(changed).IsTrue();
        await Assert.That(root.Snapshot).IsNotSameReferenceAs(originalSnapshot);
        await Assert.That(root.Snapshot.GetValue("key")).IsEqualTo("after");
        await Assert.That(originalSnapshot.GetValue("key")).IsEqualTo("before");
    }

    [Test]
    public async Task ReloadAsync_WithoutDataChange_KeepsCurrentSnapshot()
    {
        var provider = new MockProvider(
            [
                new Dictionary<string, string> { ["key"] = "same" },
                new Dictionary<string, string> { ["key"] = "same" },
            ]
        );
        var root = new CfgRoot([provider]);
        var originalSnapshot = root.Snapshot;

        var changed = await root.ReloadAsync();

        await Assert.That(changed).IsFalse();
        await Assert.That(root.Snapshot).IsSameReferenceAs(originalSnapshot);
    }

    [Test]
    public async Task ReloadAsync_WhenProviderReloadFails_PublishesObservedProviderStateBeforeRethrowing()
    {
        var provider1 = new MockProvider(
            [
                new Dictionary<string, string> { ["key"] = "before" },
                new Dictionary<string, string> { ["key"] = "after" },
            ]
        );
        var provider2 = new FailingReloadProvider();
        var root = new CfgRoot([provider1, provider2]);
        var originalSnapshot = root.Snapshot;
        var changeSignal = root.GetChangeSignal();

        await Assert.That(async () => await root.ReloadAsync()).Throws<InvalidOperationException>();
        await Assert.That(root.Snapshot).IsNotSameReferenceAs(originalSnapshot);
        await Assert.That(root.Snapshot.GetValue("key")).IsEqualTo("after");
        await Assert.That(changeSignal.HasChanged).IsTrue();
        await Assert.That(originalSnapshot.GetValue("key")).IsEqualTo("before");
    }

    [Test]
    public async Task ReloadAsync_RunsProvidersInParallel()
    {
        var coordination = new ParallelReloadCoordination(expectedConcurrentReloads: 2);
        var provider1 = new ConcurrentReloadProvider("first", "value1", coordination);
        var provider2 = new ConcurrentReloadProvider("second", "value2", coordination);
        var root = new CfgRoot([provider1, provider2]);

        var changed = await root.ReloadAsync();

        await Assert.That(changed).IsTrue();
        await Assert.That(provider1.MaxConcurrentReloads).IsEqualTo(1);
        await Assert.That(provider2.MaxConcurrentReloads).IsEqualTo(1);
        await Assert.That(coordination.MaxObservedConcurrentReloads).IsEqualTo(2);
        await Assert.That(root.Snapshot.GetValue("first")).IsEqualTo("value1");
        await Assert.That(root.Snapshot.GetValue("second")).IsEqualTo("value2");
    }

    [Test]
    public async Task ReloadAsync_ConcurrentCalls_AreSerialized()
    {
        var provider = new GatedReloadProvider();
        var root = new CfgRoot([provider]);

        var reload1 = root.ReloadAsync().AsTask();
        await provider.WaitForFirstEntryAsync();
        var reload2 = root.ReloadAsync().AsTask();

        await provider.AllowFirstReloadToComplete();
        await reload1;
        await reload2;

        await Assert.That(provider.MaxConcurrentReloads).IsEqualTo(1);
    }

    [Test]
    public async Task GetChangeSignal_ChangesWhenRootReloadUpdatesSnapshot()
    {
        var provider = new MockProvider(
            [
                new Dictionary<string, string> { ["key"] = "before" },
                new Dictionary<string, string> { ["key"] = "after" },
            ]
        );
        var root = new CfgRoot([provider]);

        var changeSignal = root.GetChangeSignal();
        var changed = await root.ReloadAsync();

        await Assert.That(changed).IsTrue();
        await Assert.That(changeSignal.HasChanged).IsTrue();
    }

    [Test]
    public async Task GetChangeSignal_DoesNotChangeUntilRootSnapshotChanges()
    {
        var provider = new MockProvider([new Dictionary<string, string> { ["key"] = "value" }]);
        var root = new CfgRoot([provider]);

        var changeSignal = root.GetChangeSignal();
        provider.NotifyChanged();

        await Assert.That(changeSignal.HasChanged).IsFalse();
    }

    [Test]
    public async Task GetChangeSignal_ReturnsFreshSignalAfterReload()
    {
        var provider = new MockProvider(
            [
                new Dictionary<string, string> { ["key"] = "before" },
                new Dictionary<string, string> { ["key"] = "after" },
            ]
        );
        var root = new CfgRoot([provider]);

        var changeSignal1 = root.GetChangeSignal();
        var changed = await root.ReloadAsync();
        var changeSignal2 = root.GetChangeSignal();

        await Assert.That(changed).IsTrue();
        await Assert.That(changeSignal1.HasChanged).IsTrue();
        await Assert.That(changeSignal2.HasChanged).IsFalse();
        await Assert.That(changeSignal2).IsNotSameReferenceAs(changeSignal1);
    }

    [Test]
    public async Task DisposeAsync_DisposesProviders()
    {
        var provider = new MockProvider([new Dictionary<string, string> { ["key"] = "value" }]);
        var root = new CfgRoot([provider]);

        await root.DisposeAsync();

        await Assert.That(provider.DisposeCalled).IsTrue();
    }

    [Test]
    public async Task DisposeAsync_WhenProviderDisposeFails_DisposesRemainingProviders()
    {
        var firstProvider = new MockProvider(
            [new Dictionary<string, string> { ["first"] = "value" }],
            disposeException: new InvalidOperationException("First dispose failed.")
        );
        var secondProvider = new MockProvider(
            [new Dictionary<string, string> { ["second"] = "value" }],
            disposeException: new InvalidOperationException("Second dispose failed.")
        );
        var root = new CfgRoot([firstProvider, secondProvider]);

        var dispose = async () => await root.DisposeAsync();

        await Assert.That(dispose).Throws<AggregateException>();
        await Assert.That(firstProvider.DisposeCalled).IsTrue();
        await Assert.That(secondProvider.DisposeCalled).IsTrue();
    }

    [Test]
    public async Task DisposeAsync_WhenSingleProviderDisposeFails_RethrowsOriginalException()
    {
        var provider = new MockProvider(
            [new Dictionary<string, string> { ["key"] = "value" }],
            disposeException: new InvalidOperationException("Dispose failed.")
        );
        var root = new CfgRoot([provider]);

        await Assert.That(async () => await root.DisposeAsync()).Throws<InvalidOperationException>();
        await Assert.That(provider.DisposeCalled).IsTrue();
    }

    [Test]
    public async Task ReloadAsync_WhenProviderCancellationOccurs_PublishesObservedProviderStateBeforeRethrowing()
    {
        using var cts = new CancellationTokenSource();
        var provider1 = new MockProvider(
            [
                new Dictionary<string, string> { ["key"] = "before" },
                new Dictionary<string, string> { ["key"] = "after" },
            ]
        );
        var provider2 = new CancelingReloadProvider(cts);
        var root = new CfgRoot([provider1, provider2]);
        var originalSnapshot = root.Snapshot;
        var changeSignal = root.GetChangeSignal();

        await Assert.That(async () => await root.ReloadAsync(cts.Token)).Throws<OperationCanceledException>();
        await Assert.That(root.Snapshot).IsNotSameReferenceAs(originalSnapshot);
        await Assert.That(root.Snapshot.GetValue("key")).IsEqualTo("after");
        await Assert.That(changeSignal.HasChanged).IsTrue();
        await Assert.That(originalSnapshot.GetValue("key")).IsEqualTo("before");
    }

    [Test]
    public async Task ReloadAsync_WhenLaterProviderThrowsSynchronously_WaitsForStartedReloadsBeforePublishing()
    {
        var provider1 = new DeferredPublishingProvider("key", "before", "after");
        var provider2 = new FailingReloadProvider();
        var root = new CfgRoot([provider1, provider2]);
        var originalSnapshot = root.Snapshot;

        var reloadTask = root.ReloadAsync().AsTask();
        await provider1.WaitForReloadStartedAsync();
        await Assert.That(reloadTask.IsCompleted).IsFalse();

        provider1.CompleteReload();

        await Assert.That(async () => await reloadTask).Throws<InvalidOperationException>();
        await Assert.That(root.Snapshot).IsNotSameReferenceAs(originalSnapshot);
        await Assert.That(root.Snapshot.GetValue("key")).IsEqualTo("after");
        await Assert.That(originalSnapshot.GetValue("key")).IsEqualTo("before");
    }

    [Test]
    public async Task DisposeAsync_ConcurrentCalls_DisposeProvidersOnlyOnce()
    {
        var provider = new CountingDisposeProvider();
        var root = new CfgRoot([provider]);

        await Task.WhenAll(root.DisposeAsync().AsTask(), root.DisposeAsync().AsTask(), root.DisposeAsync().AsTask());

        await Assert.That(provider.DisposeCount).IsEqualTo(1);
    }

    [Test]
    public async Task ReloadAsync_WhenProviderSequenceChangesButVisibleValueStaysOverridden_PublishesNewSnapshot()
    {
        var provider1 = new MockProvider(
            [
                new Dictionary<string, string> { ["shared"] = "first-before" },
                new Dictionary<string, string> { ["shared"] = "first-after" },
            ]
        );
        var provider2 = new MockProvider([new Dictionary<string, string> { ["shared"] = "second" }]);
        var root = new CfgRoot([provider1, provider2]);
        var originalSnapshot = root.Snapshot;

        var changed = await root.ReloadAsync();

        await Assert.That(changed).IsTrue();
        await Assert.That(root.Snapshot).IsNotSameReferenceAs(originalSnapshot);
        await Assert.That(root.Snapshot.GetValue("shared")).IsEqualTo("second");
    }

    [Test]
    public async Task Snapshot_WithCustomSnapshots_UsesCompositeLookupSemantics()
    {
        var provider1 = new StaticProvider(new DelegatingSnapshot(path => path == "shared" ? "first" : null));
        var provider2 = new StaticProvider(new DelegatingSnapshot(path => path == "shared" ? "second" : null));
        var root = new CfgRoot([provider1, provider2]);

        await Assert.That(root.Snapshot.GetValue("shared")).IsEqualTo("second");
    }

    private sealed class MockProvider : ICfgProvider
    {
        private readonly IReadOnlyList<IReadOnlyDictionary<string, string>> _snapshots;
        private readonly Exception? _disposeException;
        private int _index;
        private ControllableMockChangeSignal _changeSignal = new();

        public MockProvider(
            IReadOnlyList<IReadOnlyDictionary<string, string>> snapshots,
            Exception? disposeException = null
        )
        {
            _snapshots = snapshots;
            _disposeException = disposeException;
            Snapshot = new MockSnapshot(_snapshots[0]);
        }

        public int ReloadCount { get; private set; }
        public bool DisposeCalled { get; private set; }
        public ICfgSnapshot Snapshot { get; private set; }

        public ValueTask<bool> ReloadAsync(CancellationToken ct = default)
        {
            ReloadCount++;
            if (_index < _snapshots.Count - 1)
            {
                var nextValues = _snapshots[_index + 1];
                if (Snapshot is not MockSnapshot currentSnapshot)
                    throw new InvalidOperationException("Unexpected snapshot implementation.");

                _index++;
                if (!ConfigDataComparer.Equals(currentSnapshot.Values, nextValues))
                {
                    var oldSignal = _changeSignal;
                    Snapshot = new MockSnapshot(nextValues);
                    _changeSignal = new ControllableMockChangeSignal();
                    oldSignal.NotifyChanged();
                    return ValueTask.FromResult(true);
                }
            }

            return ValueTask.FromResult(false);
        }

        public ICfgChangeSignal GetChangeSignal()
        {
            return _changeSignal;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCalled = true;

            if (_disposeException is not null)
                return ValueTask.FromException(_disposeException);

            return ValueTask.CompletedTask;
        }

        public void NotifyChanged()
        {
            _changeSignal.NotifyChanged();
        }
    }

    private sealed class FailingReloadProvider : ICfgProvider
    {
        public ICfgSnapshot Snapshot { get; } = new MockSnapshot(new Dictionary<string, string>());

        public ValueTask<bool> ReloadAsync(CancellationToken ct = default)
        {
            throw new InvalidOperationException("Reload failed.");
        }

        public ICfgChangeSignal GetChangeSignal() => new ControllableMockChangeSignal();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ConcurrentReloadProvider(string key, string value, ParallelReloadCoordination coordination) : ICfgProvider
    {
        private int _activeReloads;

        public int MaxConcurrentReloads { get; private set; }
        public ICfgSnapshot Snapshot { get; private set; } = new MockSnapshot(new Dictionary<string, string>());

        public async ValueTask<bool> ReloadAsync(CancellationToken ct = default)
        {
            var active = Interlocked.Increment(ref _activeReloads);
            var globalActive = coordination.Enter();
            UpdateMax(active);

            try
            {
                await coordination.WaitForAllEnteredAsync(ct);
                Snapshot = new MockSnapshot(new Dictionary<string, string> { [key] = value });
                return true;
            }
            finally
            {
                Interlocked.Decrement(ref _activeReloads);
                coordination.Exit();
            }
        }

        public ICfgChangeSignal GetChangeSignal() => new ControllableMockChangeSignal();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private void UpdateMax(int active)
        {
            if (active > MaxConcurrentReloads)
                MaxConcurrentReloads = active;
            coordination.RecordObserved();
        }
    }

    private sealed class ParallelReloadCoordination(int expectedConcurrentReloads)
    {
        private readonly TaskCompletionSource _allEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _activeReloads;
        private int _enteredReloads;

        public int MaxObservedConcurrentReloads { get; private set; }

        public int Enter()
        {
            var active = Interlocked.Increment(ref _activeReloads);
            if (Interlocked.Increment(ref _enteredReloads) == expectedConcurrentReloads)
                _allEntered.TrySetResult();

            return active;
        }

        public Task WaitForAllEnteredAsync(CancellationToken ct) => _allEntered.Task.WaitAsync(ct);

        public void Exit() => Interlocked.Decrement(ref _activeReloads);

        public void RecordObserved()
        {
            var current = _activeReloads;
            if (current > MaxObservedConcurrentReloads)
                MaxObservedConcurrentReloads = current;
        }
    }

    private sealed class CancelingReloadProvider(CancellationTokenSource cancellationSource) : ICfgProvider
    {
        public ICfgSnapshot Snapshot { get; } = new MockSnapshot(new Dictionary<string, string>());

        public ValueTask<bool> ReloadAsync(CancellationToken ct = default)
        {
            cancellationSource.Cancel();
            ct.ThrowIfCancellationRequested();
            return ValueTask.FromResult(false);
        }

        public ICfgChangeSignal GetChangeSignal() => new ControllableMockChangeSignal();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class CountingDisposeProvider : ICfgProvider
    {
        public int DisposeCount { get; private set; }
        public ICfgSnapshot Snapshot { get; } = new MockSnapshot(new Dictionary<string, string>());

        public ValueTask<bool> ReloadAsync(CancellationToken ct = default) => ValueTask.FromResult(false);

        public ICfgChangeSignal GetChangeSignal() => new ControllableMockChangeSignal();

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class DeferredPublishingProvider(string key, string before, string after) : ICfgProvider
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _reloaded;

        public ICfgSnapshot Snapshot { get; private set; } = new MockSnapshot(
            new Dictionary<string, string> { [key] = before }
        );

        public async ValueTask<bool> ReloadAsync(CancellationToken ct = default)
        {
            if (_reloaded)
                return false;

            _entered.TrySetResult();
            await _release.Task.WaitAsync(ct);

            _reloaded = true;
            Snapshot = new MockSnapshot(new Dictionary<string, string> { [key] = after });
            return true;
        }

        public ICfgChangeSignal GetChangeSignal() => new ControllableMockChangeSignal();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task WaitForReloadStartedAsync() => _entered.Task;

        public void CompleteReload() => _release.TrySetResult();
    }

    private sealed class StaticProvider(ICfgSnapshot snapshot) : ICfgProvider
    {
        public ICfgSnapshot Snapshot { get; } = snapshot;

        public ValueTask<bool> ReloadAsync(CancellationToken ct = default) => ValueTask.FromResult(false);

        public ICfgChangeSignal GetChangeSignal() => new ControllableMockChangeSignal();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class DelegatingSnapshot(Func<string, string?> resolver) : ICfgSnapshot
    {
        public bool TryGetValue(string path, out string? value)
        {
            value = resolver(path);
            return value is not null;
        }
    }

    private sealed class GatedReloadProvider : ICfgProvider
    {
        private int _activeReloads;
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int MaxConcurrentReloads { get; private set; }
        public ICfgSnapshot Snapshot { get; } = new MockSnapshot(new Dictionary<string, string>());

        public async ValueTask<bool> ReloadAsync(CancellationToken ct = default)
        {
            var active = Interlocked.Increment(ref _activeReloads);
            if (active > MaxConcurrentReloads)
                MaxConcurrentReloads = active;

            _entered.TrySetResult();

            try
            {
                await _release.Task.WaitAsync(ct);
                return false;
            }
            finally
            {
                Interlocked.Decrement(ref _activeReloads);
            }
        }

        public ICfgChangeSignal GetChangeSignal() => new ControllableMockChangeSignal();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task WaitForFirstEntryAsync() => _entered.Task;

        public ValueTask AllowFirstReloadToComplete()
        {
            _release.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class MockSnapshot(IReadOnlyDictionary<string, string> values) : ICfgSnapshot
    {
        public IReadOnlyDictionary<string, string> Values { get; } = values;

        public bool TryGetValue(string path, out string? value)
        {
            if (Values.TryGetValue(path, out var existingValue))
            {
                value = existingValue;
                return true;
            }

            value = null;
            return false;
        }
    }

    private sealed class ControllableMockChangeSignal : ICfgChangeSignal
    {
        private CancellationTokenSource _cts = new();

        public bool HasChanged { get; private set; }

        public void NotifyChanged()
        {
            HasChanged = true;
            _cts.Cancel();
        }

        public ValueTask WaitForChangeAsync(CancellationToken ct = default)
        {
            return new ValueTask(WaitInternalAsync(ct));
        }

        private async Task WaitInternalAsync(CancellationToken ct)
        {
            if (HasChanged)
                return;

            var signalTask = _cts.Token.AwaitCancellationAsync(throwOnCancellation: false);
            if (!ct.CanBeCanceled)
            {
                await signalTask;
                return;
            }

            var cancellationTask = ct.AwaitCancellationAsync();
            var completedTask = await Task.WhenAny(signalTask, cancellationTask);
            await completedTask;
        }
    }
}
