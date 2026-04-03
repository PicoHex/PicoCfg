namespace PicoCfg.Tests;

public class StreamCfgTests
{
    [Test]
    public async Task StreamCFGSource_BuildProviderAsync_ReturnsProvider()
    {
        var streamFactory = () =>
            new MemoryStream(Encoding.UTF8.GetBytes("key1=value1\nkey2=value2"));
        var source = new StreamCfgSource(streamFactory);

        var provider = await source.OpenAsync();

        await Assert.That(provider).IsNotNull();
        await Assert.That(provider).IsTypeOf<StreamCfgProvider>();
    }

    [Test]
    public async Task StreamCFGProvider_LoadAsync_ParsesKeyValuePairs()
    {
        var streamFactory = () =>
            new MemoryStream(Encoding.UTF8.GetBytes("key1=value1\nkey2=value2\nkey3=value3"));
        var provider = new StreamCfgProvider(streamFactory);

        await provider.ReloadAsync();

        var value1 = provider.Snapshot.GetValue("key1");
        var value2 = provider.Snapshot.GetValue("key2");
        var value3 = provider.Snapshot.GetValue("key3");

        await Assert.That(value1).IsEqualTo("value1");
        await Assert.That(value2).IsEqualTo("value2");
        await Assert.That(value3).IsEqualTo("value3");
    }

    [Test]
    public async Task StreamCFGProvider_LoadAsync_IgnoresEmptyLines()
    {
        var streamFactory = () =>
            new MemoryStream(Encoding.UTF8.GetBytes("\nkey1=value1\n\nkey2=value2\n"));
        var provider = new StreamCfgProvider(streamFactory);

        await provider.ReloadAsync();

        var value1 = provider.Snapshot.GetValue("key1");
        var value2 = provider.Snapshot.GetValue("key2");

        await Assert.That(value1).IsEqualTo("value1");
        await Assert.That(value2).IsEqualTo("value2");
    }

    [Test]
    public async Task StreamCFGProvider_LoadAsync_IgnoresMalformedLines()
    {
        var streamFactory = () =>
            new MemoryStream(Encoding.UTF8.GetBytes("key1=value1\nmalformed\nkey2=value2\nkey3"));
        var provider = new StreamCfgProvider(streamFactory);

        await provider.ReloadAsync();

        var value1 = provider.Snapshot.GetValue("key1");
        var value2 = provider.Snapshot.GetValue("key2");
        var value3 = provider.Snapshot.GetValue("key3");
        var malformedValue = provider.Snapshot.GetValue("malformed");

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
        var provider = new StreamCfgProvider(streamFactory);

        await provider.ReloadAsync();

        var value1 = provider.Snapshot.GetValue("key1");
        var value2 = provider.Snapshot.GetValue("key2");

        await Assert.That(value1).IsEqualTo("value1");
        await Assert.That(value2).IsEqualTo("value2");
    }

    [Test]
    public async Task StreamCFGProvider_GetValueAsync_ReturnsNullForMissingKey()
    {
        var streamFactory = () => new MemoryStream(Encoding.UTF8.GetBytes("key1=value1"));
        var provider = new StreamCfgProvider(streamFactory);

        await provider.ReloadAsync();

        var value = provider.Snapshot.GetValue("missing");

        await Assert.That(value).IsNull();
    }

    [Test]
    public async Task StreamCFGProvider_ReloadAsync_ReplacesSnapshotData()
    {
        var currentContent = "key1=oldvalue\nkey2=value2";
        var provider = new StreamCfgProvider(
            () => new MemoryStream(Encoding.UTF8.GetBytes(currentContent))
        );

        await provider.ReloadAsync();

        var oldValue = provider.Snapshot.GetValue("key1");
        await Assert.That(oldValue).IsEqualTo("oldvalue");

        currentContent = "key1=newvalue\nkey3=value3";

        await provider.ReloadAsync();

        var newValue = provider.Snapshot.GetValue("key1");
        var key2Value = provider.Snapshot.GetValue("key2");
        var key3Value = provider.Snapshot.GetValue("key3");

        await Assert.That(newValue).IsEqualTo("newvalue");
        await Assert.That(key2Value).IsNull();
        await Assert.That(key3Value).IsEqualTo("value3");
    }

    [Test]
    public async Task StreamCFGProvider_Snapshot_IsUpdatedAfterReload()
    {
        var streamFactory = () => new MemoryStream(Encoding.UTF8.GetBytes("key1=value1"));
        var provider = new StreamCfgProvider(streamFactory);

        await provider.ReloadAsync();

        await Assert.That(provider.Snapshot.GetValue("key1")).IsEqualTo("value1");
    }

    [Test]
    public async Task StreamCFGProvider_WatchAsync_ReturnsChangeToken()
    {
        var streamFactory = () => new MemoryStream(Encoding.UTF8.GetBytes("key1=value1"));
        var provider = new StreamCfgProvider(streamFactory);

        var changeSignal = await provider.WatchAsync();

        await Assert.That(changeSignal).IsNotNull();
        await Assert.That(changeSignal).IsTypeOf<StreamChangeToken>();
    }

    [Test]
    public async Task StreamCFGProvider_LoadAsync_ChangesWatchTokenOnlyWhenDataChanges()
    {
        var currentContent = "key1=value1";
        var provider = new StreamCfgProvider(
            () => new MemoryStream(Encoding.UTF8.GetBytes(currentContent))
        );

        var initialSignal = await provider.WatchAsync();
        await provider.ReloadAsync();

        await Assert.That(initialSignal.HasChanged).IsTrue();

        var unchangedSignal = await provider.WatchAsync();
        await provider.ReloadAsync();
        await Assert.That(unchangedSignal.HasChanged).IsFalse();

        currentContent = "key1=value2";
        await provider.ReloadAsync();
        await Assert.That(unchangedSignal.HasChanged).IsTrue();

        var latestSignal = await provider.WatchAsync();
        await Assert.That(latestSignal.HasChanged).IsFalse();
    }

    [Test]
    public async Task StreamCFGProvider_WatchAsync_AfterLoad_WaitsForFutureChange()
    {
        var currentContent = "key1=value1";
        var provider = new StreamCfgProvider(
            () => new MemoryStream(Encoding.UTF8.GetBytes(currentContent))
        );

        await provider.ReloadAsync();
        var changeSignal = await provider.WatchAsync();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var waitTask = changeSignal.WaitForChangeAsync(cts.Token).AsTask();

        await Task.Delay(100, cts.Token);
        await Assert.That(waitTask.IsCompleted).IsFalse();

        currentContent = "key1=value2";
        await provider.ReloadAsync();

        await waitTask;
        await Assert.That(waitTask.IsCompleted).IsTrue();
    }

    [Test]
    public async Task DictionarySource_PreservesValuesWithoutTextRoundTrip()
    {
        var builder = Cfg.CreateBuilder();
        builder.Add(
            new Dictionary<string, string>
            {
                ["withEquals"] = "a=b=c",
                ["withNewLine"] = "line1\nline2",
            }
        );

        var config = await builder.BuildAsync();

        await Assert.That(config.Snapshot.GetValue("withEquals")).IsEqualTo("a=b=c");
        await Assert.That(config.Snapshot.GetValue("withNewLine")).IsEqualTo("line1\nline2");
    }
}
