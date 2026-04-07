[BenchmarkClass(Description = "Build configuration root from in-memory key-value pairs")]
public partial class BuildBenchmarks
{
    private Dictionary<string, string> _data = null!;

    [Params(10, 100, 1000)]
    public int N { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _data = new Dictionary<string, string>(N);
        for (var i = 0; i < N; i++)
            _data[$"Section:Key{i}"] = $"Value{i}";
    }

    [Benchmark(Baseline = true)]
    public void MsConfig()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(_data!)
            .Build();

        _ = config["Section:Key0"];
    }

    [Benchmark]
    public void PicoCfg()
    {
        var builder = Cfg.CreateBuilder();
        builder.Add(_data);

        var root = builder.BuildAsync().AsTask().GetAwaiter().GetResult();

        _ = root.Snapshot.GetValue("Section:Key0");

        root.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
