namespace PicoCfg;

/// <summary>
/// Collects configuration sources and builds a composed configuration root.
/// Later-added sources have higher lookup precedence in the built root.
/// </summary>
public sealed class CfgBuilder
{
    private readonly List<ICfgSource> _sources = [];

    /// <summary>
    /// Adds a source to the builder.
    /// Sources are evaluated in insertion order, and later sources override earlier ones.
    /// </summary>
    public CfgBuilder AddSource(ICfgSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _sources.Add(source);
        return this;
    }

    /// <summary>
    /// Opens all registered sources and returns a composed configuration root.
    /// The returned root owns the opened providers and should be disposed when no longer needed.
    /// </summary>
    public async ValueTask<ICfgRoot> BuildAsync(CancellationToken ct = default)
    {
        var providers = new List<ICfgProvider>();

        try
        {
            foreach (var source in _sources)
            {
                var provider = await source.OpenAsync(ct);
                providers.Add(provider);
            }

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
