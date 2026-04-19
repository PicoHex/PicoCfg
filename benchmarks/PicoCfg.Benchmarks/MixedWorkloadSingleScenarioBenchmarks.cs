[BenchmarkClass(Description = "Build configuration root and perform repeated lookups (single scenario)")]
public sealed partial class MixedWorkloadSingleScenarioBenchmarks
{
    private readonly int _n;
    private readonly int _providerCount;
    private readonly int _lookupPassCount;
    private IReadOnlyList<Dictionary<string, string>> _dataSets = null!;
    private string[] _keys = null!;

    public MixedWorkloadSingleScenarioBenchmarks(int n, int providerCount, int lookupPassCount)
    {
        _n = n;
        _providerCount = providerCount;
        _lookupPassCount = lookupPassCount;
    }

    [GlobalSetup]
    public void Setup()
    {
        var dataSets = new List<Dictionary<string, string>>(_providerCount);
        for (var providerIndex = 0; providerIndex < _providerCount; providerIndex++)
        {
            var data = new Dictionary<string, string>(_n);
            for (var i = 0; i < _n; i++)
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

        for (var pass = 0; pass < _lookupPassCount; pass++)
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
        _ = root.GetValue(_keys[0]);

        for (var pass = 0; pass < _lookupPassCount; pass++)
        {
            for (var i = 0; i < _keys.Length; i++)
                _ = root.GetValue(_keys[i]);
        }

        root.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
