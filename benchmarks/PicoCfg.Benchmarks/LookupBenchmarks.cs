[BenchmarkClass(Description = "Look up configuration values by key")]
public partial class LookupBenchmarks
{
    private IConfigurationRoot _msConfig = null!;
    private ICfgRoot _picoRoot = null!;
    private string[] _keys = null!;

    [Params(10, 100, 1000)]
    public int N { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var data = new Dictionary<string, string>(N);
        for (var i = 0; i < N; i++)
            data[$"Section:Key{i}"] = $"Value{i}";

        _keys = data.Keys.ToArray();

        _msConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(data!)
            .Build();

        var builder = Cfg.CreateBuilder();
        builder.Add(data);
        _picoRoot = builder.BuildAsync().AsTask().GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _picoRoot.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public void MsConfig()
    {
        for (var i = 0; i < _keys.Length; i++)
            _ = _msConfig[_keys[i]];
    }

    [Benchmark]
    public void PicoCfg()
    {
        for (var i = 0; i < _keys.Length; i++)
            _ = _picoRoot.Snapshot.GetValue(_keys[i]);
    }
}
