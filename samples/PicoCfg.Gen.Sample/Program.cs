using PicoCfg;
using PicoCfg.Extensions;

Console.WriteLine("=== PicoCfg.Gen AOT Binding Sample ===");

await using var root = await Cfg
    .CreateBuilder()
    .Add(new Dictionary<string, string>
    {
        ["App:Name"] = "PicoCfg.Gen",
        ["App:Enabled"] = "true",
        ["App:Count"] = "42",
        ["App:Mode"] = "Advanced",
    })
    .BuildAsync();

var settings = PicoCfgBind.Bind<AppSettings>(root, "App");

Console.WriteLine($"Name: {settings.Name}");
Console.WriteLine($"Enabled: {settings.Enabled}");
Console.WriteLine($"Count: {settings.Count}");
Console.WriteLine($"Mode: {settings.Mode}");

var existing = new AppSettings
{
    Name = "Before",
    Enabled = false,
    Count = 0,
    Mode = SampleMode.Basic,
};

PicoCfgBind.BindInto(root, existing, "App");

Console.WriteLine();
Console.WriteLine("BindInto result:");
Console.WriteLine($"Name: {existing.Name}");
Console.WriteLine($"Enabled: {existing.Enabled}");
Console.WriteLine($"Count: {existing.Count}");
Console.WriteLine($"Mode: {existing.Mode}");

return 0;

public sealed class AppSettings
{
    public string? Name { get; set; }
    public bool Enabled { get; set; }
    public int Count { get; set; }
    public SampleMode Mode { get; set; }
}

public enum SampleMode
{
    Basic,
    Advanced,
}
