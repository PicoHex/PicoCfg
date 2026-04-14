namespace PicoCfg;

internal sealed class StreamCfgProvider : ICfgProvider
{
    private readonly Func<Stream> _streamFactory;
    private readonly Func<object?>? _versionStampFactory;
    private readonly CfgProviderState _state = new();

    public StreamCfgProvider(Func<Stream> streamFactory)
        : this(streamFactory, null)
    {
    }

    public StreamCfgProvider(Func<Stream> streamFactory, Func<object?>? versionStampFactory)
    {
        ArgumentNullException.ThrowIfNull(streamFactory);
        _streamFactory = streamFactory;
        _versionStampFactory = versionStampFactory;
    }

    public ICfgSnapshot Snapshot => _state.Snapshot;

    public async ValueTask<bool> ReloadAsync(CancellationToken ct = default)
    {
        if (!_state.TryBeginReload(_versionStampFactory, ct, out var versionStamp))
            return false;

        var newData = await CreateSnapshotDataAsync(ct);
        return _state.PublishIfChanged(newData, versionStamp);
    }

    public ICfgChangeSignal GetChangeSignal() => _state.GetChangeSignal();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<Dictionary<string, string>> CreateSnapshotDataAsync(CancellationToken ct)
    {
        var stream = _streamFactory()
            ?? throw new InvalidOperationException("The stream factory returned null.");

        await using var _ = stream;
        using var reader = new StreamReader(stream);

        var newData = new Dictionary<string, string>();
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex < 0)
                continue;

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            newData[key] = value;
        }

        return newData;
    }
}
