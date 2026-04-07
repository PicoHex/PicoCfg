[BenchmarkClass(Description = "Reload configuration from in-memory source")]
public partial class ReloadBenchmarks
{
    private IConfigurationRoot _msConfig = null!;
    private ICfgRoot _picoRoot = null!;

    [Params(10, 100, 1000)]
    public int N { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var data = new Dictionary<string, string>(N);
        for (var i = 0; i < N; i++)
            data[$"Section:Key{i}"] = $"Value{i}";

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
        _msConfig.Reload();
    }

    [Benchmark]
    public void PicoCfg()
    {
        _picoRoot.ReloadAsync().AsTask().GetAwaiter().GetResult();
    }
}
