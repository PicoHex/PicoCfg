namespace PicoCfg.Tests;

public class CfgRuntimeDependencyWiringTests
{
    [Test]
    public async Task BuildAsync_WithCustomStreamParser_UsesInjectedStreamParserForBuiltInSourcePath()
    {
        var parserCalls = 0;
        var builder = Cfg
            .CreateBuilder()
            .WithStreamParser(async (stream, ct) =>
            {
                parserCalls++;
                using var reader = new StreamReader(stream);
                var content = await reader.ReadToEndAsync(ct);
                return new Dictionary<string, string> { ["parsed"] = $"custom:{content}" };
            });

        builder.Add(() => new MemoryStream(Encoding.UTF8.GetBytes("not-valid-default-format")));

        await using var root = await builder.BuildAsync();

        await Assert.That(parserCalls).IsEqualTo(1);
        await Assert.That(root.Snapshot.GetValue("parsed")).IsEqualTo("custom:not-valid-default-format");
    }

    [Test]
    public async Task BuildAsync_WithInjectedSnapshotComposer_UsesCustomComposerForInitialAndReloadedSnapshots()
    {
        var initialSnapshot = new DelegatingSnapshot(path => path == "mode" ? "initial" : null);
        var reloadedSnapshot = new DelegatingSnapshot(path => path == "mode" ? "reloaded" : null);
        var composeCalls = 0;
        var provider = new SequenceProvider(
            new CfgSnapshot(new Dictionary<string, string> { ["provider"] = "before" }),
            new CfgSnapshot(new Dictionary<string, string> { ["provider"] = "after" })
        );
        var builder = Cfg
            .CreateBuilder()
            .WithSnapshotComposer(_ => ++composeCalls == 1 ? initialSnapshot : reloadedSnapshot)
            .AddSource(new StaticSource(provider));

        await using var root = await builder.BuildAsync();

        await Assert.That(root.Snapshot).IsSameReferenceAs(initialSnapshot);

        var changed = await root.ReloadAsync();

        await Assert.That(changed).IsTrue();
        await Assert.That(composeCalls).IsEqualTo(2);
        await Assert.That(root.Snapshot).IsSameReferenceAs(reloadedSnapshot);
    }

    [Test]
    public async Task BuildAsync_WithInjectedChangeSignalFactory_UsesCustomRootSignals()
    {
        var initialSignal = new CfgChangeSignal();
        var nextSignal = new CfgChangeSignal();
        var signals = new Queue<CfgChangeSignal>([initialSignal, nextSignal]);
        var provider = new SequenceProvider(
            new CfgSnapshot(new Dictionary<string, string> { ["key"] = "before" }),
            new CfgSnapshot(new Dictionary<string, string> { ["key"] = "after" })
        );
        var builder = Cfg
            .CreateBuilder()
            .WithChangeSignalFactory(() => signals.Dequeue())
            .AddSource(new StaticSource(provider));

        await using var root = await builder.BuildAsync();

        await Assert.That(root.GetChangeSignal()).IsSameReferenceAs(initialSignal);

        var changed = await root.ReloadAsync();

        await Assert.That(changed).IsTrue();
        await Assert.That(initialSignal.HasChanged).IsTrue();
        await Assert.That(root.GetChangeSignal()).IsSameReferenceAs(nextSignal);
    }

    [Test]
    public async Task BuildAsync_DefaultComposer_UsesInjectedSnapshotFactoryForFlattenedNativeSnapshots()
    {
        var composedSnapshot = new CfgSnapshot(new Dictionary<string, string> { ["composed"] = "from-factory" });
        var builder = Cfg
            .CreateBuilder()
            .WithSnapshotFactory((_, _) => composedSnapshot);

        builder.Add(new Dictionary<string, string> { ["first"] = "one" });
        builder.Add(new Dictionary<string, string> { ["second"] = "two" });

        await using var root = await builder.BuildAsync();

        await Assert.That(root.Snapshot).IsSameReferenceAs(composedSnapshot);
        await Assert.That(root.Snapshot.GetValue("composed")).IsEqualTo("from-factory");
    }

    [Test]
    public async Task BuildAsync_WithInjectedProviderStateFactory_UsesCustomProviderStateForBuiltInDictionarySource()
    {
        var publishedSnapshot = new CfgSnapshot(new Dictionary<string, string> { ["key"] = "from-factory" });
        var builder = Cfg
            .CreateBuilder()
            .WithProviderStateFactory(() =>
                new CfgProviderState(
                    CfgBuilder.DefaultChangeSignalFactory,
                    (_, _) => publishedSnapshot
                )
            );

        builder.Add(new Dictionary<string, string> { ["key"] = "from-source" });

        await using var root = await builder.BuildAsync();

        await Assert.That(root.Snapshot).IsSameReferenceAs(publishedSnapshot);
        await Assert.That(root.Snapshot.GetValue("key")).IsEqualTo("from-factory");
    }

    [Test]
    public async Task DictionaryCfgProvider_WithInjectedProviderStateFactory_UsesExplicitStateDependencies()
    {
        var publishedSnapshot = new CfgSnapshot(new Dictionary<string, string> { ["key"] = "from-factory" });
        var initialSignal = new CfgChangeSignal();
        var nextSignal = new CfgChangeSignal();
        var signals = new Queue<CfgChangeSignal>([initialSignal, nextSignal]);
        var provider = new DictionaryCfgProvider(
            () => new Dictionary<string, string> { ["key"] = "from-source" },
            versionStampFactory: null,
            () => new CfgProviderState(() => signals.Dequeue(), (_, _) => publishedSnapshot)
        );

        await Assert.That(provider.GetChangeSignal()).IsSameReferenceAs(initialSignal);

        var changed = await provider.ReloadAsync();

        await Assert.That(changed).IsTrue();
        await Assert.That(provider.Snapshot).IsSameReferenceAs(publishedSnapshot);
        await Assert.That(provider.Snapshot.GetValue("key")).IsEqualTo("from-factory");
        await Assert.That(initialSignal.HasChanged).IsTrue();
        await Assert.That(provider.GetChangeSignal()).IsSameReferenceAs(nextSignal);
    }

    private sealed class StaticSource(ICfgProvider provider) : ICfgSource
    {
        public ValueTask<ICfgProvider> OpenAsync(CancellationToken ct = default) =>
            ValueTask.FromResult(provider);
    }

    private sealed class SequenceProvider(params ICfgSnapshot[] snapshots) : ICfgProvider
    {
        private readonly IReadOnlyList<ICfgSnapshot> _snapshots = snapshots;
        private int _index;

        public ICfgSnapshot Snapshot { get; private set; } = snapshots[0];

        public ValueTask<bool> ReloadAsync(CancellationToken ct = default)
        {
            if (_index >= _snapshots.Count - 1)
                return ValueTask.FromResult(false);

            _index++;
            Snapshot = _snapshots[_index];
            return ValueTask.FromResult(true);
        }

        public ICfgChangeSignal GetChangeSignal() => new CfgChangeSignal();

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
}
