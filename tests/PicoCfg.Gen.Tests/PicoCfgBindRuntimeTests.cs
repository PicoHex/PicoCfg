namespace PicoCfg.Gen.Tests;

using System.Diagnostics.CodeAnalysis;

public class PicoCfgBindRuntimeTests
{
    [Test]
    public async Task PicoCfgBind_RuntimeLivesInPicoCfgAssembly()
    {
        await Assert.That(typeof(PicoCfgBind).Assembly).IsSameReferenceAs(typeof(Cfg).Assembly);
        await Assert.That(typeof(PicoCfgBindRuntime).Assembly).IsSameReferenceAs(typeof(Cfg).Assembly);
    }

    [Test]
    public async Task Bind_BindsFlatScalarProperties()
    {
        await using var root = await Cfg
            .CreateBuilder()
            .Add(
                new Dictionary<string, string>
                {
                    ["Name"] = "PicoCfg",
                    ["Enabled"] = "true",
                    ["Count"] = "42",
                    ["Id"] = "5e5504ea-f1ad-40ef-887f-d8c2421f4189",
                    ["Mode"] = "Advanced",
                    ["Threshold"] = "12.5",
                    ["OptionalCount"] = "7",
                }
            )
            .BuildAsync();

        var model = PicoCfgBind.Bind<FlatSettings>(root.Snapshot);

        await Assert.That(model.Name).IsEqualTo("PicoCfg");
        await Assert.That(model.Enabled).IsTrue();
        await Assert.That(model.Count).IsEqualTo(42);
        await Assert.That(model.Id).IsEqualTo(Guid.Parse("5e5504ea-f1ad-40ef-887f-d8c2421f4189"));
        await Assert.That(model.Mode).IsEqualTo(BindMode.Advanced);
        await Assert.That(model.Threshold).IsEqualTo(12.5m);
        await Assert.That(model.OptionalCount).IsEqualTo(7);
    }

    [Test]
    public async Task Bind_SupportsSectionPrefix()
    {
        await using var root = await Cfg
            .CreateBuilder()
            .Add(
                new Dictionary<string, string>
                {
                    ["App:Name"] = "NestedOnlyByKey",
                    ["App:Enabled"] = "true",
                    ["App:Count"] = "9",
                    ["App:Id"] = "2211c2de-f8fb-45c5-bf09-ac5173cf54a1",
                    ["App:Mode"] = "Basic",
                    ["App:Threshold"] = "1.25",
                }
            )
            .BuildAsync();

        var model = PicoCfgBind.Bind<FlatSettings>(root, section: "App");

        await Assert.That(model.Name).IsEqualTo("NestedOnlyByKey");
        await Assert.That(model.Count).IsEqualTo(9);
        await Assert.That(model.Mode).IsEqualTo(BindMode.Basic);
    }

    [Test]
    public async Task Bind_InvalidConversion_ThrowsFormatException()
    {
        await using var root = await Cfg
            .CreateBuilder()
            .Add(
                new Dictionary<string, string>
                {
                    ["Name"] = "Bad",
                    ["Enabled"] = "not-bool",
                }
            )
            .BuildAsync();

        var thrown = await Assert.That(() => PicoCfgBind.Bind<FlatSettings>(root.Snapshot)).Throws<FormatException>();
        await Assert.That(thrown).IsNotNull();
        await Assert.That(thrown.Message).Contains("Enabled");
    }

    [Test]
    public async Task TryBind_InvalidConversion_ReturnsFalseAndDefaultValue()
    {
        await using var root = await Cfg
            .CreateBuilder()
            .Add(
                new Dictionary<string, string>
                {
                    ["Name"] = "Bad",
                    ["Count"] = "nope",
                }
            )
            .BuildAsync();

        var result = PicoCfgBind.TryBind<FlatSettings>(root, out var value);

        await Assert.That(result).IsFalse();
        await Assert.That(value).IsNull();
    }

    [Test]
    public async Task BindInto_OverwritesMatchingPropertiesAndLeavesMissingValuesUntouched()
    {
        await using var root = await Cfg
            .CreateBuilder()
            .Add(
                new Dictionary<string, string>
                {
                    ["Name"] = "Updated",
                    ["Count"] = "8",
                    ["Mode"] = "Advanced",
                }
            )
            .BuildAsync();

        var instance = new FlatSettings
        {
            Name = "Before",
            Enabled = true,
            Count = 1,
            Id = Guid.Parse("ca70af5d-1ab9-4c78-89d3-8af51b11db6e"),
            Mode = BindMode.Basic,
            Threshold = 3.5m,
            OptionalCount = 11,
        };

        PicoCfgBind.BindInto(root.Snapshot, instance);

        await Assert.That(instance.Name).IsEqualTo("Updated");
        await Assert.That(instance.Count).IsEqualTo(8);
        await Assert.That(instance.Mode).IsEqualTo(BindMode.Advanced);
        await Assert.That(instance.Enabled).IsTrue();
        await Assert.That(instance.Threshold).IsEqualTo(3.5m);
        await Assert.That(instance.OptionalCount).IsEqualTo(11);
    }

    [Test]
    public async Task GeneratedRegistration_WorksForBindIntoOnlyTargetWithoutCtor()
    {
        await using var root = await Cfg
            .CreateBuilder()
            .Add(new Dictionary<string, string> { ["Port"] = "8080" })
            .BuildAsync();

        var instance = CtorlessBindIntoOnly.Create(1);

        PicoCfgBind.BindInto(root, instance);

        await Assert.That(instance.Port).IsEqualTo(8080);
    }

    [Test]
    public async Task MissingGeneratedRegistration_FailsFastWithSpecificException()
    {
        await using var root = await Cfg.CreateBuilder().Add(new Dictionary<string, string>()).BuildAsync();

        var method = typeof(PicoCfgBind)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(static method => method.Name == nameof(PicoCfgBind.Bind)
                && method.IsGenericMethodDefinition
                && method.GetParameters() is [{ ParameterType: { Name: nameof(ICfgSnapshot) } }, ..]);

        var closedMethod = method.MakeGenericMethod(typeof(UnregisteredSettings));

        var thrown = await Assert.That(() => closedMethod.Invoke(null, [root.Snapshot, null])).Throws<TargetInvocationException>();
        await Assert.That(thrown).IsNotNull();
        await Assert.That(thrown.InnerException).IsNotNull();
        await Assert.That(thrown.InnerException).IsTypeOf<PicoCfgBindRegistrationException>();
        await Assert.That(thrown.InnerException!.Message).Contains("No generated PicoCfg.Gen registration was found");
    }

    [Test]
    public async Task IncompatibleGeneratedRegistration_FailsFastWithSpecificException()
    {
        PicoCfgBindRuntime.Register<SkewedSettings>(
            contractVersion: PicoCfgBindRuntime.ContractVersion + 1,
            bind: static (_, _) => new SkewedSettings(),
            tryBind: static (ICfgSnapshot snapshot, string? section, [MaybeNullWhen(false)] out SkewedSettings value) =>
            {
                _ = snapshot;
                _ = section;
                value = new SkewedSettings();
                return true;
            },
            bindInto: static (_, _, _) => { }
        );

        await using var root = await Cfg.CreateBuilder().Add(new Dictionary<string, string>()).BuildAsync();

        var thrown = await Assert.That(() => PicoCfgBind.Bind<SkewedSettings>(root.Snapshot)).Throws<PicoCfgBindRegistrationException>();
        await Assert.That(thrown).IsNotNull();
        await Assert.That(thrown.Message).Contains("incompatible");
    }

    public sealed class FlatSettings
    {
        public string? Name { get; set; }
        public bool Enabled { get; set; }
        public int Count { get; set; }
        public Guid Id { get; set; }
        public BindMode Mode { get; set; }
        public decimal Threshold { get; set; }
        public int? OptionalCount { get; set; }
    }

    public enum BindMode
    {
        Basic,
        Advanced,
    }

    public sealed class UnregisteredSettings
    {
        public string? Name { get; set; }
    }

    public sealed class SkewedSettings
    {
        public int Value { get; set; }
    }

    public sealed class CtorlessBindIntoOnly
    {
        private CtorlessBindIntoOnly() { }

        public int Port { get; set; }

        public static CtorlessBindIntoOnly Create(int port) => new() { Port = port };
    }
}
