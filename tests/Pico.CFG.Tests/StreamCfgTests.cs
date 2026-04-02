namespace Pico.CFG.Tests;

public class StreamCfgTests
{
    [Test]
    public async Task StreamCFGSource_BuildProviderAsync_ReturnsProvider()
    {
        var streamFactory = () =>
            new MemoryStream(Encoding.UTF8.GetBytes("key1=value1\nkey2=value2"));
        var source = new StreamCFGSource(streamFactory);

        var provider = await source.BuildProviderAsync();

        await Assert.That(provider).IsNotNull();
        await Assert.That(provider).IsTypeOf<StreamCFGProvider>();
    }

    [Test]
    public async Task StreamCFGProvider_LoadAsync_ParsesKeyValuePairs()
    {
        var streamFactory = () =>
            new MemoryStream(Encoding.UTF8.GetBytes("key1=value1\nkey2=value2\nkey3=value3"));
        var provider = new StreamCFGProvider(streamFactory);

        await provider.LoadAsync();

        var value1 = await provider.GetValueAsync("key1");
        var value2 = await provider.GetValueAsync("key2");
        var value3 = await provider.GetValueAsync("key3");

        await Assert.That(value1).IsEqualTo("value1");
        await Assert.That(value2).IsEqualTo("value2");
        await Assert.That(value3).IsEqualTo("value3");
    }

    [Test]
    public async Task StreamCFGProvider_LoadAsync_IgnoresEmptyLines()
    {
        var streamFactory = () =>
            new MemoryStream(Encoding.UTF8.GetBytes("\nkey1=value1\n\nkey2=value2\n"));
        var provider = new StreamCFGProvider(streamFactory);

        await provider.LoadAsync();

        var value1 = await provider.GetValueAsync("key1");
        var value2 = await provider.GetValueAsync("key2");

        await Assert.That(value1).IsEqualTo("value1");
        await Assert.That(value2).IsEqualTo("value2");
    }

    [Test]
    public async Task StreamCFGProvider_LoadAsync_IgnoresMalformedLines()
    {
        var streamFactory = () =>
            new MemoryStream(Encoding.UTF8.GetBytes("key1=value1\nmalformed\nkey2=value2\nkey3"));
        var provider = new StreamCFGProvider(streamFactory);

        await provider.LoadAsync();

        var value1 = await provider.GetValueAsync("key1");
        var value2 = await provider.GetValueAsync("key2");
        var value3 = await provider.GetValueAsync("key3");
        var malformedValue = await provider.GetValueAsync("malformed");

        await Assert.That(value1).IsEqualTo("value1");
        await Assert.That(value2).IsEqualTo("value2");
        await Assert.That(value3).IsNull();
        await Assert.That(malformedValue).IsNull();
    }

    [Test]
    public async Task StreamCFGProvider_LoadAsync_TrimsKeyAndValue()
    {
        var streamFactory = () =>
            new MemoryStream(Encoding.UTF8.GetBytes("  key1  =  value1  \n  key2=value2  "));
        var provider = new StreamCFGProvider(streamFactory);

        await provider.LoadAsync();

        var value1 = await provider.GetValueAsync("key1");
        var value2 = await provider.GetValueAsync("key2");

        await Assert.That(value1).IsEqualTo("value1");
        await Assert.That(value2).IsEqualTo("value2");
    }

    [Test]
    public async Task StreamCFGProvider_GetValueAsync_ReturnsNullForMissingKey()
    {
        var streamFactory = () => new MemoryStream(Encoding.UTF8.GetBytes("key1=value1"));
        var provider = new StreamCFGProvider(streamFactory);

        await provider.LoadAsync();

        var value = await provider.GetValueAsync("missing");

        await Assert.That(value).IsNull();
    }

    [Test]
    public async Task StreamCFGProvider_LoadAsync_OverwritesPreviousData()
    {
        var streamFactory1 = () =>
            new MemoryStream(Encoding.UTF8.GetBytes("key1=oldvalue\nkey2=value2"));
        var provider = new StreamCFGProvider(streamFactory1);

        await provider.LoadAsync();

        var oldValue = await provider.GetValueAsync("key1");
        await Assert.That(oldValue).IsEqualTo("oldvalue");

        var streamFactory2 = () =>
            new MemoryStream(Encoding.UTF8.GetBytes("key1=newvalue\nkey3=value3"));
        var provider2 = new StreamCFGProvider(streamFactory2);

        await provider2.LoadAsync();

        var newValue = await provider2.GetValueAsync("key1");
        var key2Value = await provider2.GetValueAsync("key2");
        var key3Value = await provider2.GetValueAsync("key3");

        await Assert.That(newValue).IsEqualTo("newvalue");
        await Assert.That(key2Value).IsNull();
        await Assert.That(key3Value).IsEqualTo("value3");
    }

    [Test]
    public async Task StreamCFGProvider_GetChildrenAsync_ReturnsEmpty()
    {
        var streamFactory = () => new MemoryStream(Encoding.UTF8.GetBytes("key1=value1"));
        var provider = new StreamCFGProvider(streamFactory);

        await provider.LoadAsync();

        var children = new List<ICFGNode>();
        await foreach (var child in provider.GetChildrenAsync())
        {
            children.Add(child);
        }

        await Assert.That(children).IsEmpty();
    }

    [Test]
    public async Task StreamCFGProvider_WatchAsync_ReturnsChangeToken()
    {
        var streamFactory = () => new MemoryStream(Encoding.UTF8.GetBytes("key1=value1"));
        var provider = new StreamCFGProvider(streamFactory);

        var changeToken = await provider.WatchAsync();

        await Assert.That(changeToken).IsNotNull();
        await Assert.That(changeToken).IsTypeOf<StreamChangeToken>();
    }
}
