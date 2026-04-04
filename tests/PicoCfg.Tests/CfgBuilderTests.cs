namespace PicoCfg.Tests;

public class CfgBuilderTests
{
    [Test]
    public async Task AddSource_AddsSourceToBuilder()
    {
        var builder = Cfg.CreateBuilder();
        var mockSource = new MockSource();

        var result = builder.AddSource(mockSource);

        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task AddSource_WithNullSource_ThrowsArgumentNullException()
    {
        var builder = Cfg.CreateBuilder();

        await Assert.That(() => builder.AddSource(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task BuildAsync_WithNoSources_ReturnsRootWithEmptyProviders()
    {
        var builder = Cfg.CreateBuilder();

        var root = await builder.BuildAsync();

        await Assert.That(root).IsNotNull();
        await Assert.That(root.Snapshot.GetValue("missing")).IsNull();
    }

    [Test]
    public async Task BuildAsync_WithOneSource_UsesSourceSnapshot()
    {
        var builder = Cfg.CreateBuilder();
        var mockSource = new MockSource();
        builder.AddSource(mockSource);

        var root = await builder.BuildAsync();

        await Assert.That(root.Snapshot.GetValue("sourceKey")).IsEqualTo("sourceValue");
    }

    [Test]
    public async Task BuildAsync_WithMultipleSources_UsesLastSourceValue()
    {
        var builder = Cfg.CreateBuilder();
        var mockSource1 = new MockSource("shared", "first");
        var mockSource2 = new MockSource("shared", "second");
        builder.AddSource(mockSource1);
        builder.AddSource(mockSource2);

        var root = await builder.BuildAsync();

        await Assert.That(root.Snapshot.GetValue("shared")).IsEqualTo("second");
    }

    [Test]
    public async Task BuildAsync_WhenLaterSourceFails_DisposesAlreadyOpenedProviders()
    {
        var builder = Cfg.CreateBuilder();
        var firstProvider = new TrackingProvider("first", "value");
        var secondProvider = new TrackingProvider("second", "value");

        builder.AddSource(new TrackingSource(firstProvider));
        builder.AddSource(new TrackingSource(secondProvider));
        builder.AddSource(new FailingSource());

        await Assert.That(async () => await builder.BuildAsync()).Throws<InvalidOperationException>();
        await Assert.That(firstProvider.DisposeCalled).IsTrue();
        await Assert.That(secondProvider.DisposeCalled).IsTrue();
    }

    private class MockSource(string key = "sourceKey", string value = "sourceValue") : ICfgSource
    {
        public ValueTask<ICfgProvider> OpenAsync(CancellationToken ct = default)
        {
            return ValueTask.FromResult<ICfgProvider>(new MockProvider(key, value));
        }
    }

    private class MockProvider(string key, string value) : ICfgProvider
    {
        public ICfgSnapshot Snapshot { get; private set; } = new MockSnapshot(key, value);

        public ValueTask<bool> ReloadAsync(CancellationToken ct = default)
        {
            return ValueTask.FromResult(false);
        }

        public ICfgChangeSignal GetChangeSignal()
        {
            return new MockChangeToken();
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private class MockSnapshot(string key, string value) : ICfgSnapshot
    {
        public bool TryGetValue(string path, out string? resolvedValue)
        {
            if (path == key)
            {
                resolvedValue = value;
                return true;
            }

            resolvedValue = null;
            return false;
        }
    }

    private class MockChangeToken : ICfgChangeSignal
    {
        public bool HasChanged => false;

        public ValueTask WaitForChangeAsync(CancellationToken ct = default)
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TrackingSource(TrackingProvider provider) : ICfgSource
    {
        public ValueTask<ICfgProvider> OpenAsync(CancellationToken ct = default)
        {
            return ValueTask.FromResult<ICfgProvider>(provider);
        }
    }

    private sealed class FailingSource : ICfgSource
    {
        public ValueTask<ICfgProvider> OpenAsync(CancellationToken ct = default)
        {
            throw new InvalidOperationException("Source open failed.");
        }
    }

    private sealed class TrackingProvider(string key, string value) : ICfgProvider
    {
        public bool DisposeCalled { get; private set; }
        public ICfgSnapshot Snapshot { get; } = new MockSnapshot(key, value);

        public ValueTask<bool> ReloadAsync(CancellationToken ct = default)
        {
            return ValueTask.FromResult(false);
        }

        public ICfgChangeSignal GetChangeSignal()
        {
            return new MockChangeToken();
        }

        public ValueTask DisposeAsync()
        {
            DisposeCalled = true;
            return ValueTask.CompletedTask;
        }
    }
}
