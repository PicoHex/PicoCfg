namespace PicoCfg.Gen.Tests;

#pragma warning disable CS0618

public class PicoCfgBindGeneratorDiagnosticsTests
{
    [Test]
    public async Task UnsupportedComplexProperty_ProducesDiagnostic()
    {
        var diagnostics = await CompileAndGetDiagnosticsAsync(
            """
            using PicoCfg;
            using PicoCfg.Abs;

            public sealed class ComplexChild
            {
                public int Value { get; set; }
            }

            public sealed class ComplexSettings
            {
                public ComplexChild? Child { get; set; }
            }

            public static class Entry
            {
                public static ComplexSettings Run(ICfgSnapshot snapshot) => CfgBind.Bind<ComplexSettings>(snapshot);
            }
            """
        );

        await AssertDiagnosticAsync(diagnostics, "PCFGGEN003");
    }

    [Test]
    public async Task UnsupportedCollectionProperty_ProducesDiagnostic()
    {
        var diagnostics = await CompileAndGetDiagnosticsAsync(
            """
            using System.Collections.Generic;
            using PicoCfg;
            using PicoCfg.Abs;

            public sealed class CollectionSettings
            {
                public List<int> Values { get; set; } = new();
            }

            public static class Entry
            {
                public static CollectionSettings Run(ICfgSnapshot snapshot) => CfgBind.Bind<CollectionSettings>(snapshot);
            }
            """
        );

        await AssertDiagnosticAsync(diagnostics, "PCFGGEN004");
    }

    [Test]
    public async Task MissingPublicParameterlessConstructor_ProducesDiagnostic()
    {
        var diagnostics = await CompileAndGetDiagnosticsAsync(
            """
            using PicoCfg;
            using PicoCfg.Abs;

            public sealed class NoCtorSettings
            {
                public NoCtorSettings(int value) => Value = value;
                public int Value { get; set; }
            }

            public static class Entry
            {
                public static NoCtorSettings Run(ICfgSnapshot snapshot) => CfgBind.Bind<NoCtorSettings>(snapshot);
            }
            """
        );

        await AssertDiagnosticAsync(diagnostics, "PCFGGEN002");
    }

    [Test]
    public async Task OpenGenericUsage_ProducesDiagnostic()
    {
        var diagnostics = await CompileAndGetDiagnosticsAsync(
            """
            using PicoCfg;
            using PicoCfg.Abs;

            public sealed class GenericSettings<T>
            {
                public string? Name { get; set; }
            }

            public static class Entry<T>
            {
                public static GenericSettings<T> Run(ICfgSnapshot snapshot) => CfgBind.Bind<GenericSettings<T>>(snapshot);
            }
            """
        );

        await AssertDiagnosticAsync(diagnostics, "PCFGGEN001");
    }

    private static async Task AssertDiagnosticAsync(ImmutableArray<Diagnostic> diagnostics, string id)
    {
        var match = diagnostics.FirstOrDefault(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error && diagnostic.Id == id);
        await Assert.That(diagnostics.Any(diagnostic => diagnostic.Id == id)).IsTrue();
        await Assert.That(match).IsNotNull();
        await Assert.That(match.Id).IsEqualTo(id);
    }

    private static async Task<ImmutableArray<Diagnostic>> CompileAndGetDiagnosticsAsync(string source)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);
        var references = GetMetadataReferences();

        var compilation = CSharpCompilation.Create(
            assemblyName: "PicoCfg.Gen.Tests.DynamicCompilation",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var generator = new PicoCfg.Gen.Generator.PicoCfgBindGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create([generator.AsSourceGenerator()], parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

        var runResult = driver.GetRunResult();
        return outputCompilation.GetDiagnostics().AddRange(runResult.Diagnostics);
    }

    private static MetadataReference[] GetMetadataReferences()
    {
        var trustedPlatformAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        var explicitAssemblies = new[]
        {
            typeof(ICfgSnapshot).Assembly.Location,
            typeof(PicoCfgBind).Assembly.Location,
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
