namespace Pico.CFG.Tests;

public class CfgRootTests
{
    [Test]
    public async Task GetValueAsync_WithNoProviders_ReturnsNull()
    {
        var root = new CFGRoot(Array.Empty<ICFGProvider>());

        var value = await root.GetValueAsync("key");

        await Assert.That(value).IsNull();
    }

    [Test]
    public async Task GetValueAsync_WithProviderReturningValue_ReturnsValue()
    {
        var mockProvider = new MockProviderWithValue("key", "value");
        var root = new CFGRoot(new[] { mockProvider });

        var value = await root.GetValueAsync("key");

        await Assert.That(value).IsEqualTo("value");
    }

    [Test]
    public async Task GetValueAsync_WithMultipleProviders_ReturnsLastNonNullValue()
    {
        var mockProvider1 = new MockProviderWithValue("key", null);
        var mockProvider2 = new MockProviderWithValue("key", "value2");
        var mockProvider3 = new MockProviderWithValue("key", "value3");
        var root = new CFGRoot(new[] { mockProvider1, mockProvider2, mockProvider3 });

        var value = await root.GetValueAsync("key");

        await Assert.That(value).IsEqualTo("value3");
    }

    [Test]
    public async Task GetValueAsync_ProvidersOrderedReverse_ReturnsLastProviderValue()
    {
        var mockProvider1 = new MockProviderWithValue("key", "value1");
        var mockProvider2 = new MockProviderWithValue("key", "value2");
        var root = new CFGRoot(new[] { mockProvider1, mockProvider2 });

        var value = await root.GetValueAsync("key");

        await Assert.That(value).IsEqualTo("value2");
    }

    [Test]
    public async Task ReloadAsync_CallsLoadOnEachProvider()
    {
        var mockProvider1 = new TrackableMockProvider();
        var mockProvider2 = new TrackableMockProvider();
        var root = new CFGRoot(new[] { mockProvider1, mockProvider2 });

        await root.ReloadAsync();

        await Assert.That(mockProvider1.LoadCalled).IsTrue();
        await Assert.That(mockProvider2.LoadCalled).IsTrue();
    }

    [Test]
    public async Task GetChildrenAsync_WithNoProviders_ReturnsEmpty()
    {
        var root = new CFGRoot(Array.Empty<ICFGProvider>());

        var children = new List<ICFGNode>();
        await foreach (var child in root.GetChildrenAsync())
        {
            children.Add(child);
        }

        await Assert.That(children).IsEmpty();
    }

    [Test]
    public async Task GetChildrenAsync_WithProviders_ReturnsAllChildren()
    {
        var mockProvider1 = new MockProviderWithChildren("child1", "child2");
        var mockProvider2 = new MockProviderWithChildren("child3", "child4");
        var root = new CFGRoot(new[] { mockProvider1, mockProvider2 });

        var children = new List<ICFGNode>();
        await foreach (var child in root.GetChildrenAsync())
        {
            children.Add(child);
        }

        await Assert.That(children).Count().IsEqualTo(4);
        await Assert.That(children.Select(c => (c as MockNode)?.Name)).Contains("child1");
        await Assert.That(children.Select(c => (c as MockNode)?.Name)).Contains("child2");
        await Assert.That(children.Select(c => (c as MockNode)?.Name)).Contains("child3");
        await Assert.That(children.Select(c => (c as MockNode)?.Name)).Contains("child4");
    }

    [Test]
    public async Task WatchAsync_ReturnsCompositeChangeToken()
    {
        var mockProvider = new MockProviderWithChangeToken();
        var root = new CFGRoot(new[] { mockProvider });

        var changeToken = await root.WatchAsync();

        await Assert.That(changeToken).IsNotNull();
    }

    [Test]
    public async Task WatchAsync_CompositeChangeToken_ReflectsProviderChanges()
    {
        var mockProvider = new MockProviderWithChangeToken();
        var root = new CFGRoot(new[] { mockProvider });

        await root.ReloadAsync();
        var changeToken = await root.WatchAsync();

        await Assert.That(changeToken.HasChanged).IsFalse();

        mockProvider.ChangeToken.NotifyChanged();

        await Assert.That(changeToken.HasChanged).IsTrue();
    }

    [Test]
    public async Task ReloadAsync_UpdatesChangeToken()
    {
        var mockProvider = new MockProviderWithChangeToken();
        var root = new CFGRoot(new[] { mockProvider });

        var changeToken1 = await root.WatchAsync();
        await Assert.That(changeToken1).IsNotNull();

        await root.ReloadAsync();

        var changeToken2 = await root.WatchAsync();
        await Assert.That(changeToken2).IsNotNull();

        await Assert.That(changeToken2.HasChanged).IsFalse();
    }

    [Test]
    public async Task CompositeChangeToken_WaitForChangeAsync_CompletesWhenAnyProviderChanges()
    {
        var mockProvider1 = new MockProviderWithChangeToken();
        var mockProvider2 = new MockProviderWithChangeToken();
        var root = new CFGRoot(new[] { mockProvider1, mockProvider2 });

        await root.ReloadAsync();
        var changeToken = await root.WatchAsync();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var waitTask = changeToken.WaitForChangeAsync(cts.Token).AsTask();

        await Task.Delay(100);
        await Assert.That(waitTask.IsCompleted).IsFalse();

        mockProvider1.ChangeToken.NotifyChanged();

        await waitTask;
        await Assert.That(waitTask.IsCompleted).IsTrue();
    }

    private class MockProviderWithValue : ICFGProvider
    {
        private readonly string _key;
        private readonly string? _value;

        public MockProviderWithValue(string key, string? value)
        {
            _key = key;
            _value = value;
        }

        public ValueTask LoadAsync(CancellationToken ct = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask<string?> GetValueAsync(string key, CancellationToken ct = default)
        {
            return ValueTask.FromResult(key == _key ? _value : null);
        }

        public async IAsyncEnumerable<ICFGNode> GetChildrenAsync(
            [EnumeratorCancellation] CancellationToken ct = default
        )
        {
            await Task.CompletedTask;
            yield break;
        }

        public ValueTask<IAsyncChangeToken> WatchAsync(CancellationToken ct = default)
        {
            return ValueTask.FromResult<IAsyncChangeToken>(new MockChangeToken());
        }
    }

    private class TrackableMockProvider : ICFGProvider
    {
        public bool LoadCalled { get; private set; }

        public ValueTask LoadAsync(CancellationToken ct = default)
        {
            LoadCalled = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask<string?> GetValueAsync(string key, CancellationToken ct = default)
        {
            return ValueTask.FromResult<string?>(null);
        }

        public async IAsyncEnumerable<ICFGNode> GetChildrenAsync(
            [EnumeratorCancellation] CancellationToken ct = default
        )
        {
            await Task.CompletedTask;
            yield break;
        }

        public ValueTask<IAsyncChangeToken> WatchAsync(CancellationToken ct = default)
        {
            return ValueTask.FromResult<IAsyncChangeToken>(new MockChangeToken());
        }
    }

    private class MockProviderWithChildren : ICFGProvider
    {
        private readonly string[] _childNames;

        public MockProviderWithChildren(params string[] childNames)
        {
            _childNames = childNames;
        }

        public ValueTask LoadAsync(CancellationToken ct = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask<string?> GetValueAsync(string key, CancellationToken ct = default)
        {
            return ValueTask.FromResult<string?>(null);
        }

        public async IAsyncEnumerable<ICFGNode> GetChildrenAsync(
            [EnumeratorCancellation] CancellationToken ct = default
        )
        {
            foreach (var name in _childNames)
            {
                await Task.CompletedTask;
                yield return new MockNode(name);
            }
        }

        public ValueTask<IAsyncChangeToken> WatchAsync(CancellationToken ct = default)
        {
            return ValueTask.FromResult<IAsyncChangeToken>(new MockChangeToken());
        }
    }

    private class MockNode : ICFGNode
    {
        public string Name { get; }

        public MockNode(string name)
        {
            Name = name;
        }

        public ValueTask<string?> GetValueAsync(string key, CancellationToken ct = default)
        {
            return ValueTask.FromResult<string?>(null);
        }

        public async IAsyncEnumerable<ICFGNode> GetChildrenAsync(
            [EnumeratorCancellation] CancellationToken ct = default
        )
        {
            await Task.CompletedTask;
            yield break;
        }

        public ValueTask<IAsyncChangeToken> WatchAsync(CancellationToken ct = default)
        {
            return ValueTask.FromResult<IAsyncChangeToken>(new MockChangeToken());
        }
    }

    private class MockProviderWithChangeToken : ICFGProvider
    {
        public ControllableMockChangeToken ChangeToken { get; } = new();

        public ValueTask LoadAsync(CancellationToken ct = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask<string?> GetValueAsync(string key, CancellationToken ct = default)
        {
            return ValueTask.FromResult<string?>(null);
        }

        public async IAsyncEnumerable<ICFGNode> GetChildrenAsync(
            [EnumeratorCancellation] CancellationToken ct = default
        )
        {
            await Task.CompletedTask;
            yield break;
        }

        public ValueTask<IAsyncChangeToken> WatchAsync(CancellationToken ct = default)
        {
            return ValueTask.FromResult<IAsyncChangeToken>(ChangeToken);
        }
    }

    private class ControllableMockChangeToken : IAsyncChangeToken
    {
        private bool _hasChanged;
        private CancellationTokenSource _cts = new();

        public bool HasChanged => _hasChanged;

        public void NotifyChanged()
        {
            _hasChanged = true;
            _cts.Cancel();
        }

        public void Reset()
        {
            _hasChanged = false;
            if (_cts.IsCancellationRequested)
            {
                var oldCts = _cts;
                _cts = new CancellationTokenSource();
                oldCts.Dispose();
            }
        }

        public ValueTask WaitForChangeAsync(CancellationToken ct = default)
        {
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
            return new ValueTask(linkedCts.Token.WaitForCancellationAsync());
        }
    }

    private class MockChangeToken : IAsyncChangeToken
    {
        public bool HasChanged => false;

        public ValueTask WaitForChangeAsync(CancellationToken ct = default)
        {
            return ValueTask.CompletedTask;
        }
    }
}
