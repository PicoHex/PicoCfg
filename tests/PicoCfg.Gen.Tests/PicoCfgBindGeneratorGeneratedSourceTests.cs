namespace PicoCfg.Gen.Tests;

#pragma warning disable CS0618

public class PicoCfgBindGeneratorGeneratedSourceTests
{
    [Test]
    public async Task CfgBindEntryPoints_EmitExpectedGeneratedShape()
    {
        var generatedSource = await GenerateSourceAsync(
            """
            using PicoCfg;
            using PicoCfg.Abs;

            public sealed class AppSettings
            {
                public string? Name { get; set; }
                public int Count { get; set; }
            }

            public static class Entry
            {
                public static AppSettings Bind(ICfg cfg) => CfgBind.Bind<AppSettings>(cfg);
                public static bool TryBind(ICfg cfg, out AppSettings value) => CfgBind.TryBind<AppSettings>(cfg, out value);
                public static void BindInto(ICfg cfg, AppSettings target) => CfgBind.BindInto(cfg, target);
            }
            """
        );

        await AssertGeneratedSourceContainsAsync(
            generatedSource,
            "[global::System.Runtime.CompilerServices.ModuleInitializerAttribute]",
            "global::PicoCfg.CfgBindRuntime.Register<global::AppSettings>",
            "contractVersion: global::PicoCfg.CfgBindRuntime.ContractVersion",
            "private static global::AppSettings Bind_0",
            "private static bool TryBind_0",
            "private static void BindInto_0",
            "global::PicoCfg.CfgBindRuntime.CombinePath(section, \"Name\")",
            "global::PicoCfg.CfgBindRuntime.TryParseInt32(__raw_Count, out var __value_Count)"
        );
    }

    [Test]
    public async Task DiRegistrationEntryPoints_EmitExpectedGeneratedRegistration()
    {
        var generatedSource = await GenerateSourceAsync(
            """
            using PicoCfg.DI;
            using PicoDI;

            var container = new SvcContainer();
            container.RegisterCfgTransient<AppSettings>("App");

            public sealed class AppSettings
            {
                public string? Name { get; set; }
            }
            """
        );

        await AssertGeneratedSourceContainsAsync(
            generatedSource,
            "global::PicoCfg.CfgBindRuntime.Register<global::AppSettings>",
            "private static global::AppSettings Bind_0",
            "private static bool TryBind_0",
            "private static void BindInto_0"
        );
    }

    private static async Task AssertGeneratedSourceContainsAsync(string generatedSource, params string[] expectedFragments)
    {
        foreach (var expectedFragment in expectedFragments)
            await Assert.That(generatedSource.Contains(expectedFragment, StringComparison.Ordinal)).IsTrue();
    }

    private static async Task<string> GenerateSourceAsync(string source)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);

        var compilation = CSharpCompilation.Create(
            assemblyName: "PicoCfg.Gen.Tests.GeneratedSourceCompilation",
            syntaxTrees: [syntaxTree],
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.ConsoleApplication)
        );

        var generator = new PicoCfg.Gen.Generator.PicoCfgBindGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create([generator.AsSourceGenerator()], parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

        var generatedSource = driver
            .GetRunResult()
            .Results
            .SelectMany(static result => result.GeneratedSources)
            .Single(sourceResult => sourceResult.HintName == "PicoCfgBindRegistrations.g.cs")
            .SourceText
            .ToString();

        await Assert.That(string.IsNullOrWhiteSpace(generatedSource)).IsFalse();
        return generatedSource;
    }

    private static MetadataReference[] GetMetadataReferences()
    {
        var trustedPlatformAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        var explicitAssemblies = new[]
        {
            typeof(CfgBind).Assembly.Location,
            typeof(PicoCfg.DI.SvcContainerExtensions).Assembly.Location,
            typeof(PicoDI.SvcContainer).Assembly.Location,
        };

        return trustedPlatformAssemblies
            .Concat(explicitAssemblies)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .DistinctBy(static reference => reference.Display, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

#pragma warning restore CS0618
