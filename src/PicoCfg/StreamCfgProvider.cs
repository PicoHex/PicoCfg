namespace PicoCfg;

internal sealed class StreamCfgProvider : ICfgProvider
{
    private readonly Lock _syncRoot = new();
    private readonly Func<Stream> _streamFactory;
    private readonly Func<object?>? _versionStampFactory;
    private object? _versionStamp;
    private CfgSnapshot _snapshot = CfgSnapshot.Empty;
    private CfgChangeSignal _changeSignal = new();

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
        object? versionStamp = null;
        var versionStampFactory = _versionStampFactory;
        if (versionStampFactory is not null)
        {
            lock (_syncRoot)
            {
                versionStamp = versionStampFactory();
                if (Equals(_versionStamp, versionStamp))
                    return false;
            }
        }

        var stream = _streamFactory()
            ?? throw new InvalidOperationException("The stream factory returned null.");

        await using var _ = stream;
        using var reader = new StreamReader(stream);

        var newData = new Dictionary<string, string>();
        var fingerprint = 14695981039346656037UL;
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
            fingerprint += ComputeHash(key, value);
        }

        return PublishSnapshot(newData, fingerprint, versionStamp);
    }

    public ICfgChangeSignal GetChangeSignal()
    {
        lock (_syncRoot)
            return _changeSignal;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private bool PublishSnapshot(Dictionary<string, string> newData, ulong fingerprint, object? versionStamp)
    {
        CfgChangeSignal? changedSignal = null;
        lock (_syncRoot)
        {
            _versionStamp = versionStamp;

            if (ConfigDataComparer.Equals(_snapshot, newData, fingerprint))
                return false;

            _snapshot = new CfgSnapshot(newData, fingerprint);
            changedSignal = _changeSignal;
            _changeSignal = new CfgChangeSignal();
        }

        changedSignal.NotifyChanged();
        return true;
    }

    private static ulong ComputeHash(string key, string value)
    {
        var hash = new HashCode();
        hash.Add(key, StringComparer.Ordinal);
        hash.Add(value, StringComparer.Ordinal);
        return unchecked((ulong)hash.ToHashCode());
    }
}
