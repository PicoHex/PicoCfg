using PicoCfg;
using PicoCfg.Abs;
using PicoCfg.DI;
using PicoCfg.Extensions;
using PicoDI;
using PicoDI.Abs;

Console.WriteLine("=== PicoCfg.DI Sample ===");

await using var root = await Cfg
    .CreateBuilder()
    .Add(new Dictionary<string, string>
    {
        ["App:Name"] = "PicoCfg.DI",
        ["App:Count"] = "42",
        ["Request:Name"] = "Scoped Request",
        ["Request:Count"] = "7",
    })
    .BuildAsync();

await using var container = new SvcContainer(autoConfigureFromGenerator: false);

container
    .RegisterCfgRoot(root)
    .RegisterCfgSingleton<AppSettings>("App")
    .RegisterCfgScoped<RequestSettings>("Request");

using var scope = container.CreateScope();
var snapshot = scope.GetService<ICfgSnapshot>();
var app = scope.GetService<AppSettings>();
var request1 = scope.GetService<RequestSettings>();
var request2 = scope.GetService<RequestSettings>();

Console.WriteLine($"Snapshot App:Name = {snapshot.GetValue("App:Name")}");
Console.WriteLine($"Singleton Name = {app.Name}, Count = {app.Count}");
Console.WriteLine($"Scoped Name = {request1.Name}, Count = {request1.Count}");
Console.WriteLine($"Scoped Same Instance = {ReferenceEquals(request1, request2)}");

return 0;

public sealed class AppSettings
{
    public string? Name { get; set; }
    public int Count { get; set; }
}

public sealed class RequestSettings
{
    public string? Name { get; set; }
    public int Count { get; set; }
}
