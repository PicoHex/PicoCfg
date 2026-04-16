namespace PicoCfg;

internal sealed class StreamCfgSource : ICfgSource
{
    private readonly Func<Stream> _streamFactory;
    private readonly Func<object?>? _versionStampFactory;
    private readonly Func<
        Func<Stream, CancellationToken, Task<Dictionary<string, string>>>
    > _streamParserFactory;
    private readonly Func<CfgProviderState> _providerStateFactory;

    public StreamCfgSource(Func<Stream> streamFactory)
        : this(
            streamFactory,
            null,
            static () => CfgBuilder.DefaultStreamParser,
            CfgBuilder.CreateDefaultProviderState
        )
    {
    }

    public StreamCfgSource(Func<Stream> streamFactory, Func<object?>? versionStampFactory)
        : this(
            streamFactory,
            versionStampFactory,
            static () => CfgBuilder.DefaultStreamParser,
            CfgBuilder.CreateDefaultProviderState
        )
    {
    }

    internal StreamCfgSource(
        Func<Stream> streamFactory,
        Func<object?>? versionStampFactory,
        Func<Func<Stream, CancellationToken, Task<Dictionary<string, string>>>> streamParserFactory,
        Func<CfgProviderState> providerStateFactory
    )
    {
        ArgumentNullException.ThrowIfNull(streamFactory);
        ArgumentNullException.ThrowIfNull(streamParserFactory);
        ArgumentNullException.ThrowIfNull(providerStateFactory);
        _streamFactory = streamFactory;
        _versionStampFactory = versionStampFactory;
        _streamParserFactory = streamParserFactory;
        _providerStateFactory = providerStateFactory;
    }

    public async ValueTask<ICfgProvider> OpenAsync(CancellationToken ct = default)
    {
        return await CfgSourceHelpers.OpenAsync(
            new StreamCfgProvider(
                _streamFactory,
                _versionStampFactory,
                _streamParserFactory(),
                _providerStateFactory
            ),
            ct
        );
    }
}
