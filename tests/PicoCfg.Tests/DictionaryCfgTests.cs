namespace PicoCfg.Tests;

public class DictionaryCfgTests
{
    [Test]
    public async Task DictionaryCfgProvider_ReloadAsync_WithVersionStampUnchanged_SkipsDataFactory()
    {
        var calls = 0;
        var value = "before";
        var provider = new DictionaryCfgProvider(
            () =>
            {
                calls++;
                return new Dictionary<string, string> { ["key"] = value };
            },
            () => 1
        );

        var initialChanged = await provider.ReloadAsync();
        value = "after";
        var changed = await provider.ReloadAsync();

        await Assert.That(initialChanged).IsTrue();
        await Assert.That(changed).IsFalse();
        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(provider.Snapshot.GetValue("key")).IsEqualTo("before");
    }

    [Test]
    public async Task DictionaryCfgSource_WithVersionStampUnchanged_SkipsReloadWork()
    {
        var calls = 0;
        var value = "before";
        var source = new DictionaryCfgSource(
            () =>
            {
                calls++;
                return new Dictionary<string, string> { ["key"] = value };
            },
            () => 1
        );

        var provider = await source.OpenAsync();
        value = "after";
        var changed = await provider.ReloadAsync();

        await Assert.That(changed).IsFalse();
        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(provider.Snapshot.GetValue("key")).IsEqualTo("before");
    }

    [Test]
    public async Task DictionaryCfgProvider_ReloadAsync_WithChangedVersionStampAndSameContent_KeepsSnapshot()
    {
        var stamp = 1;
        var provider = new DictionaryCfgProvider(
            () => new Dictionary<string, string> { ["key"] = "value" },
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
    public async Task DictionaryCfgProvider_ReloadAsync_WithChangedVersionStampAndChangedContent_PublishesNewSnapshot()
    {
        var stamp = 1;
        var value = "before";
        var provider = new DictionaryCfgProvider(
            () => new Dictionary<string, string> { ["key"] = value },
            () => stamp
        );

        var initialChanged = await provider.ReloadAsync();
        var originalSnapshot = provider.Snapshot;
        stamp = 2;
        value = "after";
        var changed = await provider.ReloadAsync();

        await Assert.That(initialChanged).IsTrue();
        await Assert.That(changed).IsTrue();
        await Assert.That(provider.Snapshot).IsNotSameReferenceAs(originalSnapshot);
        await Assert.That(provider.Snapshot.GetValue("key")).IsEqualTo("after");
    }

    [Test]
    public async Task DictionaryCfgProvider_ReloadAsync_WithDuplicateKeysAndSameVisibleState_KeepsSnapshot()
    {
        var items = new[]
        {
            new KeyValuePair<string, string>("key", "first"),
            new KeyValuePair<string, string>("key", "value"),
        };
        var provider = new DictionaryCfgProvider(() => items);

        var initialChanged = await provider.ReloadAsync();
        var originalSnapshot = provider.Snapshot;

        items =
        [
            new KeyValuePair<string, string>("key", "different"),
            new KeyValuePair<string, string>("key", "value"),
        ];

        var changed = await provider.ReloadAsync();

        await Assert.That(initialChanged).IsTrue();
        await Assert.That(changed).IsFalse();
        await Assert.That(provider.Snapshot).IsSameReferenceAs(originalSnapshot);
        await Assert.That(provider.Snapshot.GetValue("key")).IsEqualTo("value");
    }

    [Test]
    public async Task DictionaryCfgProvider_ReloadAsync_CallsVersionStampFactoryOutsideLock()
    {
        DictionaryCfgProvider? provider = null;
        provider = new DictionaryCfgProvider(
            () => new Dictionary<string, string> { ["key"] = "value" },
            () =>
            {
                var snapshotTask = Task.Run(() => provider!.Snapshot.GetValue("key"));
                if (!snapshotTask.Wait(TimeSpan.FromSeconds(1)))
                    throw new TimeoutException("Version stamp factory ran while provider lock was held.");

                return 1;
            }
        );

        var changed = await provider.ReloadAsync();

        await Assert.That(changed).IsTrue();
    }
}
