namespace Pico.CFG.Tests;

public class CfgBuilderTests
{
    [Test]
    public async Task AddSource_AddsSourceToBuilder()
    {
        var builder = CFG.CreateBuilder();
        var mockSource = new MockSource();

        var result = builder.AddSource(mockSource);

        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task BuildAsync_WithNoSources_ReturnsRootWithEmptyProviders()
    {
        var builder = CFG.CreateBuilder();

        var root = await builder.BuildAsync();

        await Assert.That(root).IsNotNull();
        await Assert.That(root.Providers).IsEmpty();
    }

    [Test]
    public async Task BuildAsync_WithOneSource_ReturnsRootWithOneProvider()
    {
        var builder = CFG.CreateBuilder();
        var mockSource = new MockSource();
        builder.AddSource(mockSource);

        var root = await builder.BuildAsync();

        await Assert.That(root.Providers).Count().IsEqualTo(1);
    }

    [Test]
    public async Task BuildAsync_WithMultipleSources_ReturnsRootWithMultipleProviders()
    {
        var builder = CFG.CreateBuilder();
        var mockSource1 = new MockSource();
        var mockSource2 = new MockSource();
        builder.AddSource(mockSource1);
        builder.AddSource(mockSource2);

        var root = await builder.BuildAsync();

        await Assert.That(root.Providers).Count().IsEqualTo(2);
    }

    private class MockSource : ICFGSource
    {
        public ValueTask<ICFGProvider> BuildProviderAsync(CancellationToken ct = default)
        {
            return ValueTask.FromResult<ICFGProvider>(new MockProvider());
        }
    }

    private class MockProvider : ICFGProvider
    {
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
            return ValueTask.FromResult<IAsyncChangeToken>(new MockChangeToken());
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
