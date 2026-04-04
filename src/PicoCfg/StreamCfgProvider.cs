namespace PicoCfg;

internal sealed class StreamCfgProvider : ICfgProvider
{
    private readonly Lock _syncRoot = new();
    private readonly Func<Stream> _streamFactory;
    private CfgSnapshot _snapshot = CfgSnapshot.Empty;
    private CfgChangeSignal _changeSignal = new();

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

    public async ValueTask<bool> ReloadAsync(CancellationToken ct = default)
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

        return PublishSnapshot(newData);
    }

    public ICfgChangeSignal GetChangeSignal()
    {
        lock (_syncRoot)
            return _changeSignal;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private bool PublishSnapshot(Dictionary<string, string> newData)
    {
        CfgChangeSignal? changedSignal = null;
        lock (_syncRoot)
        {
            if (ConfigDataComparer.Equals(_snapshot.Values, newData))
                return false;

            _snapshot = new CfgSnapshot(newData);
            changedSignal = _changeSignal;
            _changeSignal = new CfgChangeSignal();
        }

        changedSignal.NotifyChanged();
        return true;
    }
}
