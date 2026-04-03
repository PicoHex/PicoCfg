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

        public ValueTask ReloadAsync(CancellationToken ct = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask<ICfgChangeSignal> WatchAsync(CancellationToken ct = default)
        {
            return ValueTask.FromResult<ICfgChangeSignal>(new MockChangeToken());
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
}
