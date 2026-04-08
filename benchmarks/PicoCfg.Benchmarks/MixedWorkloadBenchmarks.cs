[BenchmarkClass(Description = "Build configuration root and perform repeated lookups")]
public partial class MixedWorkloadBenchmarks
{
    private IReadOnlyList<Dictionary<string, string>> _dataSets = null!;
    private string[] _keys = null!;

    [Params(1000)]
    public int N { get; set; }

    [Params(4)]
    public int ProviderCount { get; set; }

    [Params(0, 10)]
    public int LookupPassCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var dataSets = new List<Dictionary<string, string>>(ProviderCount);
        for (var providerIndex = 0; providerIndex < ProviderCount; providerIndex++)
        {
            var data = new Dictionary<string, string>(N);
            for (var i = 0; i < N; i++)
                data[$"Section:Key{i}"] = $"Provider{providerIndex}:Value{i}";

            dataSets.Add(data);
        }

        _dataSets = dataSets;
        _keys = _dataSets[^1].Keys.ToArray();
    }

    [Benchmark(Baseline = true)]
    public void MsConfig()
    {
        var builder = new ConfigurationBuilder();
        for (var i = 0; i < _dataSets.Count; i++)
        {
            builder.AddInMemoryCollection(
                _dataSets[i].ToDictionary(static pair => pair.Key, static pair => (string?)pair.Value)
            );
        }

        var config = builder.Build();
        _ = config[_keys[0]];

        for (var pass = 0; pass < LookupPassCount; pass++)
        {
            for (var i = 0; i < _keys.Length; i++)
                _ = config[_keys[i]];
        }
    }

    [Benchmark]
    public void PicoCfg()
    {
        var builder = Cfg.CreateBuilder();
        for (var i = 0; i < _dataSets.Count; i++)
            builder.Add(_dataSets[i]);

        var root = builder.BuildAsync().AsTask().GetAwaiter().GetResult();
        var snapshot = root.Snapshot;
        _ = snapshot.GetValue(_keys[0]);

        for (var pass = 0; pass < LookupPassCount; pass++)
        {
            for (var i = 0; i < _keys.Length; i++)
                _ = snapshot.GetValue(_keys[i]);
        }

        root.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
