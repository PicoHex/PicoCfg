namespace PicoCfg;

internal sealed class StreamCfgSource : ICfgSource
{
    private readonly Func<Stream> _streamFactory;
    private readonly Func<object?>? _versionStampFactory;

    public StreamCfgSource(Func<Stream> streamFactory)
        : this(streamFactory, null)
    {
    }

    public StreamCfgSource(Func<Stream> streamFactory, Func<object?>? versionStampFactory)
    {
        ArgumentNullException.ThrowIfNull(streamFactory);
        _streamFactory = streamFactory;
        _versionStampFactory = versionStampFactory;
    }

    public async ValueTask<ICfgProvider> OpenAsync(CancellationToken ct = default)
    {
        var provider = new StreamCfgProvider(_streamFactory, _versionStampFactory);
        await provider.ReloadAsync(ct);
        return provider;
    }
}
