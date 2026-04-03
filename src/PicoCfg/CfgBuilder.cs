namespace PicoCfg;

public sealed class CfgBuilder
{
    private readonly List<ICfgSource> _sources = [];

    public CfgBuilder AddSource(ICfgSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _sources.Add(source);
        return this;
    }

    public async ValueTask<ICfgRoot> BuildAsync(CancellationToken ct = default)
    {
        var providers = new List<ICfgProvider>();

        try
        {
            foreach (var source in _sources)
                providers.Add(await source.OpenAsync(ct));

            return new CfgRoot(providers);
        }
        catch
        {
            await DisposeProvidersAsync(providers);
            throw;
        }
    }

    private static async ValueTask DisposeProvidersAsync(IReadOnlyList<ICfgProvider> providers)
    {
        for (var i = providers.Count - 1; i >= 0; i--)
        {
            try
            {
                await providers[i].DisposeAsync();
            }
            catch
            {
                // Preserve the original build failure while still attempting full cleanup.
            }
        }
    }
}
