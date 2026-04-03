namespace PicoCfg;

public class StreamCfgProvider : ICfgProvider
{
    private readonly Lock _syncRoot = new();
    private readonly Func<Stream> _streamFactory;
    private CfgSnapshot _snapshot = CfgSnapshot.Empty;
    private StreamChangeToken _changeToken = new();

    public StreamCfgProvider(Func<Stream> streamFactory)
    {
        ArgumentNullException.ThrowIfNull(streamFactory);
        _streamFactory = streamFactory;
    }

    public ICfgSnapshot Snapshot
    {
        get
        {
            lock (_syncRoot)
                return _snapshot;
        }
    }

    public async ValueTask ReloadAsync(CancellationToken ct = default)
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

            var pair = line.Split('=', 2);
            if (pair.Length is 2)
                newData[pair[0].Trim()] = pair[1].Trim();
        }

        PublishSnapshot(newData);
    }

    public ValueTask<ICfgChangeSignal> WatchAsync(CancellationToken ct = default)
    {
        lock (_syncRoot)
            return ValueTask.FromResult<ICfgChangeSignal>(_changeToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private void PublishSnapshot(Dictionary<string, string> newData)
    {
        StreamChangeToken? changedToken = null;
        lock (_syncRoot)
        {
            if (ConfigDataComparer.Equals(_snapshot.Values, newData))
                return;

            _snapshot = new CfgSnapshot(newData);
            changedToken = _changeToken;
            _changeToken = new StreamChangeToken();
        }

        changedToken.NotifyChanged();
    }
}
