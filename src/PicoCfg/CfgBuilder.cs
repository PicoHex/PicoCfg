namespace PicoCfg;

public sealed class CfgBuilder
{
    private readonly List<ICfgSource> _sources = [];

    public CfgBuilder AddSource(ICfgSource source)
    {
        _sources.Add(source);
        return this;
    }

    public async ValueTask<ICfgRoot> BuildAsync(CancellationToken ct = default)
    {
        var providers = new List<ICfgProvider>();
        foreach (var source in _sources)
            providers.Add(await source.OpenAsync(ct));
        return new CfgRoot(providers);
    }
}
