// PicoCfg Benchmarks — compares PicoCfg with Microsoft.Extensions.Configuration

var buildSuite = BenchmarkRunner.Run<BuildBenchmarks>(BenchmarkConfig.Default);
var lookupSuite = BenchmarkRunner.Run<LookupBenchmarks>(BenchmarkConfig.Default);
var reloadSuite = BenchmarkRunner.Run<ReloadBenchmarks>(BenchmarkConfig.Default);

var formatter = new ConsoleFormatter();
Console.WriteLine(formatter.Format(buildSuite));
Console.WriteLine(formatter.Format(lookupSuite));
Console.WriteLine(formatter.Format(reloadSuite));

if (buildSuite.Comparisons is not null)
    Console.WriteLine(SummaryFormatter.Format(buildSuite.Comparisons));

if (lookupSuite.Comparisons is not null)
    Console.WriteLine(SummaryFormatter.Format(lookupSuite.Comparisons));

if (reloadSuite.Comparisons is not null)
    Console.WriteLine(SummaryFormatter.Format(reloadSuite.Comparisons));

var outputDir = Path.Combine(AppContext.BaseDirectory, "results");
Directory.CreateDirectory(outputDir);

var mdFormatter = new MarkdownFormatter();
File.WriteAllText(
    Path.Combine(outputDir, "results.md"),
    mdFormatter.Format(buildSuite)
    + Environment.NewLine
    + mdFormatter.Format(lookupSuite)
    + Environment.NewLine
    + mdFormatter.Format(reloadSuite)
);

Console.WriteLine($"\nResults saved to: {outputDir}");
