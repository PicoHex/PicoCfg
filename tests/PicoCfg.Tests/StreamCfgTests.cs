namespace PicoCfg.Tests;

public class StreamCfgTests
{
    [Test]
    public async Task StreamCfgSource_WithNullFactory_ThrowsArgumentNullException()
    {
        await Assert.That(() => new StreamCfgSource(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task StreamCfgProvider_WithNullFactory_ThrowsArgumentNullException()
    {
        await Assert.That(() => new StreamCfgProvider(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task StreamCfgSource_OpenAsync_ReturnsProvider()
    {
        var streamFactory = () =>
            new MemoryStream(Encoding.UTF8.GetBytes("key1=value1\nkey2=value2"));
        var source = new StreamCfgSource(streamFactory);

        var provider = await source.OpenAsync();

        await Assert.That(provider).IsNotNull();
        await Assert.That(provider).IsAssignableTo<ICfgProvider>();
    }

    [Test]
    public async Task StreamCfgProvider_ReloadAsync_ParsesKeyValuePairs()
    {
        var streamFactory = () =>
            new MemoryStream(Encoding.UTF8.GetBytes("key1=value1\nkey2=value2\nkey3=value3"));
        var provider = new StreamCfgProvider(streamFactory);

        var changed = await provider.ReloadAsync();

        await Assert.That(changed).IsTrue();
        var value1 = provider.Snapshot.GetValue("key1");
        var value2 = provider.Snapshot.GetValue("key2");
        var value3 = provider.Snapshot.GetValue("key3");

        await Assert.That(value1).IsEqualTo("value1");
        await Assert.That(value2).IsEqualTo("value2");
        await Assert.That(value3).IsEqualTo("value3");
    }

    [Test]
    public async Task StreamCfgProvider_ReloadAsync_IgnoresEmptyLines()
    {
        var streamFactory = () =>
            new MemoryStream(Encoding.UTF8.GetBytes("\nkey1=value1\n\nkey2=value2\n"));
        var provider = new StreamCfgProvider(streamFactory);

        _ = await provider.ReloadAsync();

        var value1 = provider.Snapshot.GetValue("key1");
        var value2 = provider.Snapshot.GetValue("key2");

        await Assert.That(value1).IsEqualTo("value1");
        await Assert.That(value2).IsEqualTo("value2");
    }

    [Test]
    public async Task StreamCfgProvider_ReloadAsync_IgnoresMalformedLines()
    {
        var streamFactory = () =>
            new MemoryStream(Encoding.UTF8.GetBytes("key1=value1\nmalformed\nkey2=value2\nkey3"));
        var provider = new StreamCfgProvider(streamFactory);

        _ = await provider.ReloadAsync();

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
    public async Task StreamCfgProvider_ReloadAsync_TrimsKeyAndValue()
    {
        var streamFactory = () =>
            new MemoryStream(Encoding.UTF8.GetBytes("  key1  =  value1  \n  key2=value2  "));
        var provider = new StreamCfgProvider(streamFactory);

        _ = await provider.ReloadAsync();

        var value1 = provider.Snapshot.GetValue("key1");
        var value2 = provider.Snapshot.GetValue("key2");

        await Assert.That(value1).IsEqualTo("value1");
        await Assert.That(value2).IsEqualTo("value2");
    }

    [Test]
    public async Task StreamCfgProvider_GetValue_ReturnsNullForMissingKey()
    {
        var streamFactory = () => new MemoryStream(Encoding.UTF8.GetBytes("key1=value1"));
        var provider = new StreamCfgProvider(streamFactory);

        _ = await provider.ReloadAsync();

        var value = provider.Snapshot.GetValue("missing");

        await Assert.That(value).IsNull();
    }

    [Test]
    public async Task StreamCfgProvider_ReloadAsync_ReplacesSnapshotData()
    {
        var currentContent = "key1=oldvalue\nkey2=value2";
        var provider = new StreamCfgProvider(
            () => new MemoryStream(Encoding.UTF8.GetBytes(currentContent))
        );

        var initialChanged = await provider.ReloadAsync();

        await Assert.That(initialChanged).IsTrue();
        var oldValue = provider.Snapshot.GetValue("key1");
        await Assert.That(oldValue).IsEqualTo("oldvalue");

        currentContent = "key1=newvalue\nkey3=value3";

        var changed = await provider.ReloadAsync();

        await Assert.That(changed).IsTrue();
        var newValue = provider.Snapshot.GetValue("key1");
        var key2Value = provider.Snapshot.GetValue("key2");
        var key3Value = provider.Snapshot.GetValue("key3");

        await Assert.That(newValue).IsEqualTo("newvalue");
        await Assert.That(key2Value).IsNull();
        await Assert.That(key3Value).IsEqualTo("value3");
    }

    [Test]
    public async Task StreamCfgProvider_Snapshot_IsUpdatedAfterReload()
    {
        var streamFactory = () => new MemoryStream(Encoding.UTF8.GetBytes("key1=value1"));
        var provider = new StreamCfgProvider(streamFactory);

        _ = await provider.ReloadAsync();

        await Assert.That(provider.Snapshot.GetValue("key1")).IsEqualTo("value1");
    }

    [Test]
    public async Task StreamCfgProvider_GetChangeSignal_ReturnsCurrentSignal()
    {
        var streamFactory = () => new MemoryStream(Encoding.UTF8.GetBytes("key1=value1"));
        var provider = new StreamCfgProvider(streamFactory);

        var changeSignal = provider.GetChangeSignal();

        await Assert.That(changeSignal).IsNotNull();
        await Assert.That(changeSignal.HasChanged).IsFalse();
    }

    [Test]
    public async Task StreamCfgProvider_ReloadAsync_ChangesSignalOnlyWhenDataChanges()
    {
        var currentContent = "key1=value1";
        var provider = new StreamCfgProvider(
            () => new MemoryStream(Encoding.UTF8.GetBytes(currentContent))
        );

        var initialSignal = provider.GetChangeSignal();
        var initialChanged = await provider.ReloadAsync();

        await Assert.That(initialChanged).IsTrue();
        await Assert.That(initialSignal.HasChanged).IsTrue();

        var unchangedSignal = provider.GetChangeSignal();
        var unchanged = await provider.ReloadAsync();
        await Assert.That(unchanged).IsFalse();
        await Assert.That(unchangedSignal.HasChanged).IsFalse();

        currentContent = "key1=value2";
        var changed = await provider.ReloadAsync();
        await Assert.That(changed).IsTrue();
        await Assert.That(unchangedSignal.HasChanged).IsTrue();

        var latestSignal = provider.GetChangeSignal();
        await Assert.That(latestSignal.HasChanged).IsFalse();
    }

    [Test]
    public async Task StreamCfgProvider_ReloadAsync_WhenFactoryReturnsNull_ThrowsInvalidOperationException()
    {
        var provider = new StreamCfgProvider(() => null!);

        await Assert.That(async () => await provider.ReloadAsync()).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task StreamCfgProvider_GetChangeSignal_AfterReload_WaitsForFutureChange()
    {
        var currentContent = "key1=value1";
        var provider = new StreamCfgProvider(
            () => new MemoryStream(Encoding.UTF8.GetBytes(currentContent))
        );

        _ = await provider.ReloadAsync();
        var changeSignal = provider.GetChangeSignal();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var waitTask = changeSignal.WaitForChangeAsync(cts.Token).AsTask();

        await Task.Delay(100, cts.Token);
        await Assert.That(waitTask.IsCompleted).IsFalse();

        currentContent = "key1=value2";
        var changed = await provider.ReloadAsync();

        await Assert.That(changed).IsTrue();
        await waitTask;
        await Assert.That(waitTask.IsCompleted).IsTrue();
    }

    [Test]
    public async Task StreamCfgProvider_ReloadAsync_WithVersionStampUnchanged_SkipsStreamFactory()
    {
        var calls = 0;
        var content = "key1=value1";
        var provider = new StreamCfgProvider(
            () =>
            {
                calls++;
                return new MemoryStream(Encoding.UTF8.GetBytes(content));
            },
            () => 1
        );

        var initialChanged = await provider.ReloadAsync();
        content = "key1=value2";
        var changed = await provider.ReloadAsync();

        await Assert.That(initialChanged).IsTrue();
        await Assert.That(changed).IsFalse();
        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(provider.Snapshot.GetValue("key1")).IsEqualTo("value1");
    }

    [Test]
    public async Task StreamCfgProvider_ReloadAsync_WithChangedVersionStampAndSameContent_KeepsSnapshot()
    {
        var stamp = 1;
        const string content = "key1=value1";
        var provider = new StreamCfgProvider(
            () => new MemoryStream(Encoding.UTF8.GetBytes(content)),
            () => stamp
        );

        var initialChanged = await provider.ReloadAsync();
        var originalSnapshot = provider.Snapshot;
        stamp = 2;
        var changed = await provider.ReloadAsync();

        await Assert.That(initialChanged).IsTrue();
        await Assert.That(changed).IsFalse();
        await Assert.That(provider.Snapshot).IsSameReferenceAs(originalSnapshot);
    }

    [Test]
    public async Task StreamCfgProvider_ReloadAsync_WithChangedVersionStampAndChangedContent_PublishesNewSnapshot()
    {
        var stamp = 1;
        var content = "key1=value1";
        var provider = new StreamCfgProvider(
            () => new MemoryStream(Encoding.UTF8.GetBytes(content)),
            () => stamp
        );

        var initialChanged = await provider.ReloadAsync();
        var originalSnapshot = provider.Snapshot;
        stamp = 2;
        content = "key1=value2";
        var changed = await provider.ReloadAsync();

        await Assert.That(initialChanged).IsTrue();
        await Assert.That(changed).IsTrue();
        await Assert.That(provider.Snapshot).IsNotSameReferenceAs(originalSnapshot);
        await Assert.That(provider.Snapshot.GetValue("key1")).IsEqualTo("value2");
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

    [Test]
    public async Task BuilderAdd_StreamFactoryWithVersionStamp_UsesVersionStampShortCircuit()
    {
        var builder = Cfg.CreateBuilder();
        var content = "key=value1";
        var stamp = 1;
        var calls = 0;

        builder.Add(
            () =>
            {
                calls++;
                return new MemoryStream(Encoding.UTF8.GetBytes(content));
            },
            () => stamp
        );

        var root = await builder.BuildAsync();
        stamp = 1;
        content = "key=value2";
        var changed = await root.ReloadAsync();

        await Assert.That(changed).IsFalse();
        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(root.Snapshot.GetValue("key")).IsEqualTo("value1");
    }
}
