namespace PicoCfg;

internal sealed class StreamCfgSource : ICfgSource
{
    private readonly Func<Stream> _streamFactory;

    public StreamCfgSource(Func<Stream> streamFactory)
    {
        ArgumentNullException.ThrowIfNull(streamFactory);
        _streamFactory = streamFactory;
    }

    public async ValueTask<ICfgProvider> OpenAsync(CancellationToken ct = default)
    {
        var provider = new StreamCfgProvider(_streamFactory);
        await provider.ReloadAsync(ct);
        return provider;
    }
}
