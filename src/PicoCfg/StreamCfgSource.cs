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
        return await CfgSourceHelpers.OpenAsync(new StreamCfgProvider(_streamFactory, _versionStampFactory), ct);
    }
}
