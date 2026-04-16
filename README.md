# PicoCfg

[English](README.md) | [简体中文](README.zh.md) | [繁體中文](README.zh-TW.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Français](README.fr.md) | [日本語](README.ja.md) | [Português (Brasil)](README.pt-BR.md) | [Русский](README.ru.md)

[![CI](https://github.com/PicoHex/PicoCfg/actions/workflows/ci.yml/badge.svg)](https://github.com/PicoHex/PicoCfg/actions/workflows/ci.yml)

PicoCfg is a small, AOT-friendly configuration library for .NET.
It composes multiple sources into a stable read-only snapshot, supports explicit reload, and exposes a one-shot change signal for published updates.

## Why PicoCfg

- small public surface
- exact string key lookup
- explicit reload and change-signal semantics
- custom source support via `PicoCfg.Abs`
- Native AOT-friendly design

## Installation

Most applications only need `PicoCfg`:

```bash
dotnet add package PicoCfg
```

Use `PicoCfg.Abs` when you only need the contracts for custom integrations or abstractions:

```bash
dotnet add package PicoCfg.Abs
```

Use `PicoCfg.Gen` when you want AOT-safe source-generated binding from PicoCfg snapshots or roots into flat POCOs. The package adds the generator, while the `PicoCfgBind` runtime API lives in `PicoCfg`:

```bash
dotnet add package PicoCfg.Gen
```

Use `PicoCfg.DI` when you want PicoDI registration helpers for `ICfgRoot`, `ICfgSnapshot`, and generated binding-backed configuration services. In project-reference mode, keep a direct `PicoCfg.Gen` reference in the consuming app so the binder generator runs for your `RegisterCfg*<T>` calls:

```bash
dotnet add package PicoCfg.DI
dotnet add package PicoCfg.Gen
```

## Quick Start

```csharp
using System.Text;
using PicoCfg;
using PicoCfg.Extensions;

await using var root = await Cfg
    .CreateBuilder()
    .Add("ConnectionString=Host=localhost")
    .Add(new Dictionary<string, string>
    {
        ["Logging:Level"] = "Debug",
        ["FeatureFlag"] = "true",
    })
    .Add(() => new MemoryStream(Encoding.UTF8.GetBytes("AppName=PicoCfg")))
    .BuildAsync();

Console.WriteLine(root.Snapshot.GetValue("ConnectionString"));
Console.WriteLine(root.Snapshot.GetValue("Logging:Level"));
```

Later sources override earlier ones.

## Generated Binding with PicoCfg.Gen

`PicoCfg.Gen` adds the source generator for PicoCfg's exact-key snapshot model, while `PicoCfg` provides the `PicoCfgBind` runtime API used by the generated binder.
The generated binder is synchronous, trim-friendly, and designed for Native AOT scenarios.

```csharp
using PicoCfg;
using PicoCfg.Extensions;

await using var root = await Cfg
    .CreateBuilder()
    .Add(new Dictionary<string, string>
    {
        ["App:Name"] = "PicoCfg",
        ["App:Enabled"] = "true",
        ["App:Count"] = "42",
    })
    .BuildAsync();

var settings = PicoCfgBind.Bind<AppSettings>(root, "App");

Console.WriteLine(settings.Name);
Console.WriteLine(settings.Enabled);
Console.WriteLine(settings.Count);

public sealed class AppSettings
{
    public string? Name { get; set; }
    public bool Enabled { get; set; }
    public int Count { get; set; }
}
```

With `PicoCfg.Gen` referenced, the generated-binding surface is:

- `PicoCfgBind.Bind<T>(ICfgSnapshot, section?)`
- `PicoCfgBind.Bind<T>(ICfgRoot, section?)`
- `PicoCfgBind.TryBind<T>(...)`
- `PicoCfgBind.BindInto<T>(...)`

### PicoCfg.Gen v1 scope

The current generated binder intentionally stays narrow:

- direct closed generic `PicoCfgBind` calls only
- concrete class targets only
- flat public writable scalar properties only
- exact case-sensitive property-name matching
- optional `section:` prefix composition on top of PicoCfg's exact string keys

Unsupported shapes produce build diagnostics instead of runtime reflection fallback, including:

- nested or complex object properties
- collection properties
- open generic targets
- unsupported property types

`Bind<T>` and `TryBind<T>` require a public parameterless constructor.
`BindInto<T>` can still target an existing instance when you want to bind into a type without one.

The repository also includes `samples/PicoCfg.Gen.Sample` as a small end-to-end generated binding example.

## PicoCfg.DI with PicoDI

`PicoCfg.DI` adds PicoDI-friendly registration helpers on top of `PicoCfg` and `PicoCfg.Gen`.
Use `RegisterCfgRoot(...)` when you already own an `ICfgRoot`, `RegisterCfgSnapshot(...)` when you want a fixed snapshot, and `RegisterCfgTransient<T>()` / `RegisterCfgScoped<T>()` / `RegisterCfgSingleton<T>()` when you want generated bound POCOs resolved through PicoDI.

```csharp
using PicoCfg;
using PicoCfg.DI;
using PicoCfg.Extensions;
using PicoDI;
using PicoDI.Abs;

await using var root = await Cfg
    .CreateBuilder()
    .Add(new Dictionary<string, string>
    {
        ["App:Name"] = "PicoCfg.DI",
        ["App:Count"] = "42",
    })
    .BuildAsync();

await using var container = new SvcContainer(autoConfigureFromGenerator: false);

container
    .RegisterCfgRoot(root)
    .RegisterCfgSingleton<AppSettings>("App");

using var scope = container.CreateScope();
var settings = scope.GetService<AppSettings>();

Console.WriteLine(settings.Name);
Console.WriteLine(settings.Count);

public sealed class AppSettings
{
    public string? Name { get; set; }
    public int Count { get; set; }
}
```

The repository also includes `samples/PicoCfg.DI.Sample` as a small end-to-end PicoDI integration example.

## Core Semantics

### Exact key lookup

`GetValue()` performs exact full-string lookup over the current snapshot and returns `null` when the key is absent.
Characters such as `:` and `.` are part of the key name; PicoCfg does not interpret them as hierarchical traversal.

### Stable snapshots

`ICfgRoot.Snapshot` exposes the currently published read-only snapshot.
If reload does not publish a new snapshot, the same snapshot instance is retained.
Root publication follows the composed provider snapshot sequence, not only the final merged visible values.
Already obtained snapshots remain usable after root disposal.

### Lifetime

The built root owns the opened providers and implements `IAsyncDisposable`.
Prefer `await using` for normal usage.
Disposal releases owned providers, but it does not invalidate snapshots or the version-specific change signals that were already obtained.

## Source Types

### String source

```csharp
builder.Add("Key1=Value1\nKey2=Value2");
```

Parsed as line-based `key=value` content.

### Dictionary source

```csharp
builder.Add(new Dictionary<string, string>
{
    ["RawValue"] = "a=b=c",
    ["MultiLine"] = "line1\nline2",
});
```

Dictionary values are used as-is.
They are not reparsed, so embedded `=` characters and newline content are preserved.

### Stream source

```csharp
builder.Add(() => File.OpenRead("app.cfg"));
```

Reparsed on each reload using the same line-based `key=value` format as string content.

### Text and stream parsing rules

For text and stream sources:

- blank lines are ignored
- malformed lines without `=` are ignored
- only the first `=` splits key and value
- keys and values are trimmed

For example, `Key = a=b=c` produces key `Key` and value `a=b=c`.

## Reload and Change Signals

- `ReloadAsync()` returns `true` only when a new snapshot instance is published
- `ReloadAsync()` returns `false` when the current snapshot instance is retained
- `GetChangeSignal()` returns the one-shot signal for the current published version
- each `ReloadAsync()` call publishes at most one new composed snapshot
- after a published change, fetch a new signal for later waits because signals are tied to a single published version

If a reload throws or is canceled after some providers have already published new snapshot versions,
the root may first publish the observed composed snapshot for those settled provider versions after reload tasks settle, and then
rethrow the failure. After a failed reload, re-sample `Snapshot` and fetch a new change signal if you
need to observe the latest published state.

When a built-in source uses `versionStampFactory`, the first completed materialization establishes an
accepted authoritative stamp baseline. Any later completed rematerialization updates that baseline even
when the current snapshot instance is retained because the materialized content is unchanged. Later equal
stamps, including repeated `null`, skip reread, reparse, or re-enumeration work. A changed stamp forces
rematerialization, but the current snapshot may still be retained when the materialized content is unchanged.

When all composed provider snapshots are PicoCfg's native snapshot type, the root flattens them into a
single dictionary-backed snapshot for steady-state reads. If any provider supplies a custom
`ICfgSnapshot`, the root preserves that custom lookup behavior and falls back to read-time provider
scanning instead of flattening away the custom semantics. Fallback composition still honors normal
precedence: later providers override earlier ones.

## Custom Sources

Custom integrations are built on `PicoCfg.Abs`.

- `ICfgSource.OpenAsync()` opens a source into a long-lived provider
- the returned provider must already expose a readable `Snapshot`
- `ICfgProvider.ReloadAsync()` reports whether that provider published a new snapshot instance; `false`
  is authoritative unchanged for that provider version, and callers may retain the current snapshot reference
- `ICfgProvider.GetChangeSignal()` returns the one-shot signal for the current published version

Minimal sketch:

```csharp
using PicoCfg.Abs;

public sealed class CustomSource : ICfgSource
{
    public async ValueTask<ICfgProvider> OpenAsync(CancellationToken ct = default)
    {
        var provider = new CustomProvider();
        await provider.ReloadAsync(ct);
        return provider;
    }
}
```

## Custom Builder Composition

`CfgBuilder` also exposes a small builder-centric composition surface for the built-in source pipeline.
Use `WithStreamParser(...)` when you want to customize how built-in text and stream sources parse content, and use `WithSnapshotComposer(...)` when you want to customize how provider snapshots are composed into the published root snapshot.

If you want to decorate the default behavior instead of replacing it outright, wrap the public default helpers:

```csharp
var defaultParser = CfgBuilder.CreateDefaultStreamParser();
var defaultComposer = CfgBuilder.CreateDefaultSnapshotComposer();

var builder = Cfg
    .CreateBuilder()
    .WithStreamParser((stream, ct) => defaultParser(stream, ct))
    .WithSnapshotComposer(providerSnapshots => defaultComposer(providerSnapshots));
```

These hooks are intentionally narrow. Lower-level runtime composition types stay internal, and custom provider/source abstractions still belong in `PicoCfg.Abs`.

## Native AOT

PicoCfg is designed to stay friendly to Native AOT scenarios.
The repository includes `samples/PicoCfg.Sample` and CI validation using `dotnet publish -p:PublishAOT=true`.

Example:

```bash
dotnet publish samples/PicoCfg.Sample \
  -c Release \
  -r win-x64 \
  -p:PublishAOT=true \
  --self-contained
```

Adjust the runtime identifier for your target platform.

## Build and Test

These commands match the repository CI workflow:

```bash
dotnet restore tests/PicoCfg.Tests/PicoCfg.Tests.csproj -p:UseProjectReferences=true
dotnet build tests/PicoCfg.Tests/PicoCfg.Tests.csproj --configuration Release --no-restore -p:UseProjectReferences=true
dotnet test --project tests/PicoCfg.Tests/PicoCfg.Tests.csproj --configuration Release --no-build --verbosity normal -p:UseProjectReferences=true
dotnet test --project tests/PicoCfg.DI.Tests/PicoCfg.DI.Tests.csproj --configuration Release --no-build --verbosity normal -p:UseProjectReferences=true
```

Run the sample locally:

```bash
dotnet run --project samples/PicoCfg.Sample/PicoCfg.Sample.csproj
dotnet run --project samples/PicoCfg.Gen.Sample/PicoCfg.Gen.Sample.csproj
dotnet run --project samples/PicoCfg.DI.Sample/PicoCfg.DI.Sample.csproj
```

## License

MIT License.
