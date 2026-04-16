namespace PicoCfg;

internal sealed class StreamCfgProvider : ICfgProvider
{
    private readonly Func<Stream> _streamFactory;
    private readonly Func<object?>? _versionStampFactory;
    private readonly Func<Stream, CancellationToken, Task<Dictionary<string, string>>> _streamParser;
    private readonly CfgProviderState _state;

    public StreamCfgProvider(Func<Stream> streamFactory)
        : this(
            streamFactory,
            null,
            CfgBuilder.DefaultStreamParser,
            CfgBuilder.CreateDefaultProviderState
        )
    {
    }

    public StreamCfgProvider(Func<Stream> streamFactory, Func<object?>? versionStampFactory)
        : this(
            streamFactory,
            versionStampFactory,
            CfgBuilder.DefaultStreamParser,
            CfgBuilder.CreateDefaultProviderState
        )
    {
    }

    internal StreamCfgProvider(
        Func<Stream> streamFactory,
        Func<object?>? versionStampFactory,
        Func<Stream, CancellationToken, Task<Dictionary<string, string>>> streamParser,
        Func<CfgProviderState> providerStateFactory
    )
    {
        ArgumentNullException.ThrowIfNull(streamFactory);
        ArgumentNullException.ThrowIfNull(streamParser);
        ArgumentNullException.ThrowIfNull(providerStateFactory);
        _streamFactory = streamFactory;
        _versionStampFactory = versionStampFactory;
        _streamParser = streamParser;
        _state = providerStateFactory()
            ?? throw new InvalidOperationException("The provider state factory returned null.");
    }

    public ICfgSnapshot Snapshot => _state.Snapshot;

    public async ValueTask<bool> ReloadAsync(CancellationToken ct = default)
    {
        if (!_state.TryBeginReload(_versionStampFactory, ct, out var candidateVersionStamp))
            return false;

        var newData = await CreateSnapshotDataAsync(ct);
        return _state.PublishIfChanged(newData, candidateVersionStamp);
    }

    public ICfgChangeSignal GetChangeSignal() => _state.GetChangeSignal();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<Dictionary<string, string>> CreateSnapshotDataAsync(CancellationToken ct)
    {
        var stream = _streamFactory()
            ?? throw new InvalidOperationException("The stream factory returned null.");

        await using var _ = stream;
        return await _streamParser(stream, ct);
    }
}
