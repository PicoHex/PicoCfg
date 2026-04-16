namespace PicoCfg;

/// <summary>
/// Collects configuration sources and builds a composed configuration root.
/// Later-added sources have higher lookup precedence in the built root.
/// </summary>
public sealed class CfgBuilder
{
    private static readonly Func<
        Stream,
        CancellationToken,
        Task<Dictionary<string, string>>
    > s_defaultStreamParser = ParseStreamAsync;

    private static readonly Func<CfgChangeSignal> s_defaultChangeSignalFactory =
        static () => new CfgChangeSignal();

    private static readonly Func<
        IReadOnlyDictionary<string, string>,
        ulong,
        CfgSnapshot
    > s_defaultSnapshotFactory = static (values, fingerprint) => new CfgSnapshot(values, fingerprint);

    private readonly List<ICfgSource> _sources = [];
    private Func<
        Stream,
        CancellationToken,
        Task<Dictionary<string, string>>
    > _streamParser = s_defaultStreamParser;

    private Func<CfgChangeSignal> _changeSignalFactory = s_defaultChangeSignalFactory;

    private Func<
        IReadOnlyDictionary<string, string>,
        ulong,
        CfgSnapshot
    > _snapshotFactory = s_defaultSnapshotFactory;

    private Func<
        IReadOnlyList<ICfgSnapshot>,
        Func<IReadOnlyDictionary<string, string>, ulong, CfgSnapshot>,
        ICfgSnapshot
    >? _snapshotComposerOverride;

    private Func<
        Func<CfgChangeSignal>,
        Func<IReadOnlyDictionary<string, string>, ulong, CfgSnapshot>,
        CfgProviderState
    >? _providerStateFactoryOverride;

    internal static Func<
        Stream,
        CancellationToken,
        Task<Dictionary<string, string>>
    > DefaultStreamParser => s_defaultStreamParser;

    internal static Func<CfgChangeSignal> DefaultChangeSignalFactory => s_defaultChangeSignalFactory;

    internal static Func<
        IReadOnlyDictionary<string, string>,
        ulong,
        CfgSnapshot
    > DefaultSnapshotFactory => s_defaultSnapshotFactory;

    /// <summary>
    /// Adds a source to the builder.
    /// Sources are evaluated in insertion order, and later sources override earlier ones.
    /// </summary>
    public CfgBuilder AddSource(ICfgSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _sources.Add(source);
        return this;
    }

    /// <summary>
    /// Opens all registered sources and returns a composed configuration root.
    /// The returned root owns the opened providers and should be disposed when no longer needed.
    /// </summary>
    public async ValueTask<ICfgRoot> BuildAsync(CancellationToken ct = default)
    {
        var providers = new List<ICfgProvider>();

        try
        {
            foreach (var source in _sources)
            {
                var provider = await source.OpenAsync(ct);
                providers.Add(provider);
            }

            return new CfgRoot(providers, CreateSnapshotComposer(), _changeSignalFactory);
        }
        catch
        {
            await DisposeProvidersAsync(providers);
            throw;
        }
    }

    internal Func<
        Stream,
        CancellationToken,
        Task<Dictionary<string, string>>
    > CreateStreamParser() => _streamParser;

    internal CfgProviderState CreateProviderState()
    {
        var changeSignalFactory = _changeSignalFactory;
        var snapshotFactory = _snapshotFactory;
        var providerStateFactoryOverride = _providerStateFactoryOverride;

        return providerStateFactoryOverride is null
            ? new CfgProviderState(changeSignalFactory, snapshotFactory)
            : providerStateFactoryOverride(changeSignalFactory, snapshotFactory);
    }

    internal Func<IReadOnlyList<ICfgSnapshot>, ICfgSnapshot> CreateSnapshotComposer()
    {
        var snapshotFactory = _snapshotFactory;
        var snapshotComposerOverride = _snapshotComposerOverride;

        return snapshotComposerOverride is null
            ? CreateDefaultSnapshotComposer(snapshotFactory)
            : providerSnapshots => snapshotComposerOverride(providerSnapshots, snapshotFactory);
    }

    internal CfgBuilder WithStreamParser(
        Func<Stream, CancellationToken, Task<Dictionary<string, string>>> streamParser
    )
    {
        ArgumentNullException.ThrowIfNull(streamParser);
        _streamParser = streamParser;
        return this;
    }

    internal CfgBuilder WithChangeSignalFactory(Func<CfgChangeSignal> changeSignalFactory)
    {
        ArgumentNullException.ThrowIfNull(changeSignalFactory);
        _changeSignalFactory = changeSignalFactory;
        return this;
    }

    internal CfgBuilder WithSnapshotFactory(
        Func<IReadOnlyDictionary<string, string>, ulong, CfgSnapshot> snapshotFactory
    )
    {
        ArgumentNullException.ThrowIfNull(snapshotFactory);
        _snapshotFactory = snapshotFactory;
        return this;
    }

    internal CfgBuilder WithSnapshotComposer(
        Func<IReadOnlyList<ICfgSnapshot>, ICfgSnapshot> snapshotComposer
    )
    {
        ArgumentNullException.ThrowIfNull(snapshotComposer);
        _snapshotComposerOverride = (providerSnapshots, _) => snapshotComposer(providerSnapshots);
        return this;
    }

    internal CfgBuilder WithSnapshotComposer(
        Func<
            IReadOnlyList<ICfgSnapshot>,
            Func<IReadOnlyDictionary<string, string>, ulong, CfgSnapshot>,
            ICfgSnapshot
        > snapshotComposer
    )
    {
        ArgumentNullException.ThrowIfNull(snapshotComposer);
        _snapshotComposerOverride = snapshotComposer;
        return this;
    }

    internal CfgBuilder WithProviderStateFactory(Func<CfgProviderState> providerStateFactory)
    {
        ArgumentNullException.ThrowIfNull(providerStateFactory);
        _providerStateFactoryOverride = (_, _) => providerStateFactory();
        return this;
    }

    internal CfgBuilder WithProviderStateFactory(
        Func<
            Func<CfgChangeSignal>,
            Func<IReadOnlyDictionary<string, string>, ulong, CfgSnapshot>,
            CfgProviderState
        > providerStateFactory
    )
    {
        ArgumentNullException.ThrowIfNull(providerStateFactory);
        _providerStateFactoryOverride = providerStateFactory;
        return this;
    }

    internal static CfgProviderState CreateDefaultProviderState() =>
        new(DefaultChangeSignalFactory, DefaultSnapshotFactory);

    internal static Func<IReadOnlyList<ICfgSnapshot>, ICfgSnapshot> CreateDefaultSnapshotComposer(
        Func<IReadOnlyDictionary<string, string>, ulong, CfgSnapshot> snapshotFactory
    )
    {
        ArgumentNullException.ThrowIfNull(snapshotFactory);
        return providerSnapshots => CfgSnapshotComposer.CreateSnapshot(providerSnapshots, snapshotFactory);
    }

    private static async ValueTask DisposeProvidersAsync(IReadOnlyList<ICfgProvider> providers)
    {
        for (var i = providers.Count - 1; i >= 0; i--)
        {
            try
            {
                await providers[i].DisposeAsync();
            }
            catch
            {
                // Preserve the original build failure while still attempting full cleanup.
            }
        }
    }

    private static async Task<Dictionary<string, string>> ParseStreamAsync(
        Stream stream,
        CancellationToken ct
    )
    {
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
