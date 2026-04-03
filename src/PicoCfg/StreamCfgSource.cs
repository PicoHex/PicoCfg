namespace PicoCfg;

public class StreamCfgSource(Func<Stream> streamFactory) : ICfgSource
{
    public async ValueTask<ICfgProvider> OpenAsync(CancellationToken ct = default)
    {
        var provider = new StreamCfgProvider(streamFactory);
        await provider.ReloadAsync(ct);
        return provider;
    }
}
