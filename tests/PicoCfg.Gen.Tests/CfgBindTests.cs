namespace PicoCfg.Gen.Tests;

public sealed class CfgBindTests
{
    [Test]
    public async Task CfgBind_LivesInPicoCfgAssembly()
    {
        await Assert.That(typeof(CfgBind).Assembly).IsSameReferenceAs(typeof(Cfg).Assembly);
    }

    [Test]
    public async Task Bind_BindsFromSnapshot()
    {
        await using var root = await Cfg
            .CreateBuilder()
            .Add(new Dictionary<string, string> { ["Name"] = "PicoCfg", ["Count"] = "42" })
            .BuildAsync();

        var settings = CfgBind.Bind<PicoCfgBindRuntimeTests.FlatSettings>(root.Snapshot);

        await Assert.That(settings.Name).IsEqualTo("PicoCfg");
        await Assert.That(settings.Count).IsEqualTo(42);
    }

    [Test]
    public async Task Bind_BindsFromRuntimeCurrentSnapshot()
    {
        await using var root = await Cfg
            .CreateBuilder()
            .Add(new Dictionary<string, string> { ["Name"] = "Runtime", ["Count"] = "7" })
            .BuildAsync();

        var settings = CfgBind.Bind<PicoCfgBindRuntimeTests.FlatSettings>(root);

        await Assert.That(settings.Name).IsEqualTo("Runtime");
        await Assert.That(settings.Count).IsEqualTo(7);
    }

    [Test]
    public async Task Bind_BindsFromCfgWhenCfgIsSnapshot()
    {
        await using var root = await Cfg
            .CreateBuilder()
            .Add(new Dictionary<string, string> { ["Name"] = "Cfg", ["Count"] = "3" })
            .BuildAsync();

        ICfg cfg = root.Snapshot;

        var settings = CfgBind.Bind<PicoCfgBindRuntimeTests.FlatSettings>(cfg);

        await Assert.That(settings.Name).IsEqualTo("Cfg");
        await Assert.That(settings.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Bind_FromNonSnapshotCfg_FailsFast()
    {
        ICfg cfg = new InlineCfg(new Dictionary<string, string> { ["Name"] = "Nope" });

        var thrown = await Assert.That(() => CfgBind.Bind<PicoCfgBindRuntimeTests.FlatSettings>(cfg)).Throws<InvalidOperationException>();

        await Assert.That(thrown).IsNotNull();
        await Assert.That(thrown.Message).Contains(nameof(ICfgSnapshot));
    }

    private sealed class InlineCfg(IReadOnlyDictionary<string, string> values) : ICfg
    {
        public bool TryGetValue(string path, out string? value)
        {
            if (values.TryGetValue(path, out var resolved))
            {
                value = resolved;
                return true;
            }

            value = null;
            return false;
        }
    }

}
