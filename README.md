# PicoCfg

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

## Core Semantics

### Exact key lookup

`GetValue()` performs exact string lookup over the current snapshot.
Characters such as `:` and `.` are part of the key name; PicoCfg does not interpret them as hierarchical traversal.

### Stable snapshots

`ICfgRoot.Snapshot` exposes the currently published read-only snapshot.
If reload does not publish a new snapshot, the same snapshot instance is retained.
Already obtained snapshots remain usable after root disposal.

### Lifetime

The built root owns the opened providers and implements `IAsyncDisposable`.
Prefer `await using` for normal usage.
Disposal releases owned providers, but it does not invalidate snapshots or change signals that were already obtained.

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
- after a published change, fetch a new signal for later waits

If a reload throws or is canceled after some providers have already published new snapshot versions,
the root may first publish the observed composed snapshot for those settled provider versions and then
rethrow the failure. After a failed reload, re-sample `Snapshot` and fetch a new change signal if you
need to observe the latest published state.

When a built-in source uses `versionStampFactory`, the first completed materialization establishes an
authoritative stamp baseline. Later equal stamps, including repeated `null`, skip source work entirely.
A changed stamp forces the source to reread or reparse, but the current snapshot may still be retained
when the materialized content is unchanged.

When all composed provider snapshots are PicoCfg's native snapshot type, the root flattens them into a
single dictionary-backed snapshot for steady-state reads. If any provider supplies a custom
`ICfgSnapshot`, the root preserves that custom lookup behavior and falls back to read-time provider
scanning instead of flattening away the custom semantics.

## Custom Sources

Custom integrations are built on `PicoCfg.Abs`.

- `ICfgSource.OpenAsync()` opens a source into a long-lived provider
- the returned provider must already expose a readable `Snapshot`
- `ICfgProvider.ReloadAsync()` reports whether that provider published a new snapshot instance
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
```

Run the sample locally:

```bash
dotnet run --project samples/PicoCfg.Sample/PicoCfg.Sample.csproj
```

## License

MIT License.
