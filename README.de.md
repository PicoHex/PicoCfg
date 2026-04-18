# PicoCfg

[English](README.md) | [简体中文](README.zh.md) | [繁體中文](README.zh-TW.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Français](README.fr.md) | [日本語](README.ja.md) | [Português (Brasil)](README.pt-BR.md) | [Русский](README.ru.md)

[![CI](https://github.com/PicoHex/PicoCfg/actions/workflows/ci.yml/badge.svg)](https://github.com/PicoHex/PicoCfg/actions/workflows/ci.yml)

PicoCfg ist eine kleine, AOT-freundliche Konfigurationsbibliothek für .NET.
Sie kombiniert mehrere Quellen zu einem stabilen, schreibgeschützten Snapshot, unterstützt explizites Reload und stellt ein One-Shot-Change-Signal für veröffentlichte Aktualisierungen bereit.

## Warum PicoCfg

- kleine öffentliche Oberfläche
- exakte String-Key-Lookups
- explizite Reload- und Change-Signal-Semantik
- kleine Consumer-Verträge über `PicoCfg.Abs`
- Native-AOT-freundliches Design

## Installation

Die meisten Anwendungen benötigen nur `PicoCfg`:

```bash
dotnet add package PicoCfg
```

Verwende `PicoCfg.Abs`, wenn du nur die minimalen Consumer-Verträge wie `ICfg` und `ICfgRoot` benötigst:

```bash
dotnet add package PicoCfg.Abs
```

Verwende `PicoCfg.Gen`, wenn du PicoCfg-Konfigurationsansichten AOT-sicher per Source Generation in flache POCOs binden möchtest. Das Paket liefert den Generator, während die Runtime-API `CfgBind` in `PicoCfg` liegt:

```bash
dotnet add package PicoCfg.Gen
```

Verwende `PicoCfg.DI`, wenn du PicoDI-freundliche Registrierungshelfer für `ICfgRoot`, `ICfg` und Konfigurationsdienste auf Basis generierter Bindung nutzen möchtest. Im Project-Reference-Modus solltest du in der konsumierenden App einen direkten Verweis auf `PicoCfg.Gen` beibehalten, damit der Binder-Generator für deine `RegisterCfg*<T>`-Aufrufe ausgeführt wird:

```bash
dotnet add package PicoCfg.DI
dotnet add package PicoCfg.Gen
```

## Schnellstart

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

Console.WriteLine(root.GetValue("ConnectionString"));
Console.WriteLine(root.GetValue("Logging:Level"));
```

Später hinzugefügte Quellen überschreiben früher hinzugefügte Quellen.

## Generierte Bindung mit PicoCfg.Gen

`PicoCfg.Gen` liefert den Source Generator für PicoCfgs exaktes Schlüsselmodell, während `PicoCfg` die Runtime-API `CfgBind` für den erzeugten Binder bereitstellt.
Der erzeugte Binder ist synchron, trim-freundlich und für Native-AOT-Szenarien ausgelegt.

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

var settings = CfgBind.Bind<AppSettings>(root, "App");

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

Mit referenziertem `PicoCfg.Gen` steht folgende Generated-Binding-Oberfläche zur Verfügung:

- `CfgBind.Bind<T>(ICfgRoot, section?)`
- `CfgBind.Bind<T>(ICfg, section?)`
- `CfgBind.TryBind<T>(...)`
- `CfgBind.BindInto<T>(...)`

### PicoCfg.Gen v1-Grenzen

Der aktuelle generierte Binder bleibt absichtlich eng gefasst:

- nur direkte geschlossene generische `CfgBind`-Aufrufe
- nur konkrete Klassen als Ziele
- nur flache öffentliche schreibbare skalare Properties
- exaktes, case-sensitives Matching von Property-Namen
- optionales `section:`-Präfix zusätzlich zu PicoCfgs exakten String-Keys

Nicht unterstützte Formen erzeugen Build-Diagnosen statt eines Reflection-Fallbacks zur Laufzeit, darunter:

- verschachtelte oder komplexe Objekt-Properties
- Collection-Properties
- offene Generics als Ziel
- nicht unterstützte Property-Typen

`Bind<T>` und `TryBind<T>` benötigen einen öffentlichen parameterlosen Konstruktor.
`BindInto<T>` kann weiterhin in eine vorhandene Instanz ohne diesen Konstruktor schreiben.

Das Repository enthält außerdem `samples/PicoCfg.Gen.Sample` als kleines End-to-End-Beispiel für generierte Bindung.

## PicoCfg.DI mit PicoDI

`PicoCfg.DI` ergänzt `PicoCfg` und `PicoCfg.Gen` um PicoDI-freundliche Registrierungshelfer.
Verwende `RegisterCfgRoot(...)`, wenn du bereits ein `ICfgRoot` besitzt, und `RegisterCfgTransient<T>()` / `RegisterCfgScoped<T>()` / `RegisterCfgSingleton<T>()`, wenn du generiert gebundene POCOs über PicoDI auflösen möchtest.

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
var cfg = scope.GetService<ICfg>();
var settings = scope.GetService<AppSettings>();

Console.WriteLine(cfg.GetValue("App:Name"));
Console.WriteLine(settings.Name);
Console.WriteLine(settings.Count);

public sealed class AppSettings
{
    public string? Name { get; set; }
    public int Count { get; set; }
}
```

Das Repository enthält außerdem `samples/PicoCfg.DI.Sample` als kleines End-to-End-Beispiel für eine PicoDI-Integration.

## Kernsemantik

### Exakte Schlüsselsuche

`GetValue()` führt ein exaktes Full-String-Lookup auf dem aktuellen Snapshot aus und gibt `null` zurück, wenn der Schlüssel fehlt.
Zeichen wie `:` und `.` sind Teil des Schlüsselnamens; PicoCfg interpretiert sie nicht als hierarchische Traversierung.

### Veröffentlichte Konfigurationsansichten [advanced]

`ICfgRoot` liest immer aus der aktuell veröffentlichten zusammengesetzten Konfigurationsansicht.
Wenn ein Reload keine neue Ansicht veröffentlicht, beobachten Lesezugriffe weiterhin denselben veröffentlichten Zustand.
Die Veröffentlichung auf Root-Ebene folgt der zusammengesetzten Provider-Snapshot-Sequenz und nicht nur den final sichtbaren zusammengeführten Werten.

Der meiste Anwendungscode sollte bei `ICfg` für exakte Lookups, `ICfgRoot` für Ownership-, Reload- und Wait-Semantik sowie bei gebundenen POCOs für typisierten Zugriff bleiben.

### Lebensdauer

Der gebaute Root besitzt die geöffneten Provider und implementiert `IAsyncDisposable`.
Für die normale Nutzung wird `await using` empfohlen.
Dispose gibt die besessenen Provider frei, invalidiert jedoch nicht bereits erhaltene Snapshots oder versionsspezifische Change-Signale.

## Quelltypen

### String-Quelle

```csharp
builder.Add("Key1=Value1\nKey2=Value2");
```

Wird als zeilenbasierter `key=value`-Text geparst.

### Dictionary-Quelle

```csharp
builder.Add(new Dictionary<string, string>
{
    ["RawValue"] = "a=b=c",
    ["MultiLine"] = "line1\nline2",
});
```

Dictionary-Werte werden unverändert verwendet.
Sie werden nicht erneut als Text geparst, daher bleiben eingebettete `=`-Zeichen und mehrzeilige Inhalte erhalten.

### Stream-Quelle

```csharp
builder.Add(() => File.OpenRead("app.cfg"));
```

Wird bei jedem Reload mit demselben zeilenbasierten `key=value`-Format wie String-Inhalte erneut geparst.

### Text- und Stream-Parsingregeln

Für Text- und Stream-Quellen gilt:

- leere Zeilen werden ignoriert
- fehlerhafte Zeilen ohne `=` werden ignoriert
- nur das erste `=` trennt Schlüssel und Wert
- Schlüssel und Werte werden getrimmt

Zum Beispiel erzeugt `Key = a=b=c` den Schlüssel `Key` und den Wert `a=b=c`.

## Neuladen und Änderungssignale

- `ReloadAsync()` gibt nur dann `true` zurück, wenn eine neue Snapshot-Instanz veröffentlicht wird
- `ReloadAsync()` gibt `false` zurück, wenn die aktuelle Snapshot-Instanz beibehalten wird
- jeder `ReloadAsync()`-Aufruf veröffentlicht höchstens einen neuen zusammengesetzten Snapshot
- `WaitForChangeAsync()` ist die öffentliche Primitive zur Änderungsbenachrichtigung für Root-Consumer

Wenn ein Reload eine Ausnahme auslöst oder abgebrochen wird, nachdem einige Provider bereits neue Snapshot-Versionen veröffentlicht haben,
kann der Root nach dem Settlen der Reload-Tasks zuerst den beobachteten zusammengesetzten Snapshot für diese bereits gesettleten Provider-Versionen veröffentlichen
und anschließend den Fehler erneut auslösen. Nach einem fehlgeschlagenen Reload solltest du über `ICfgRoot` oder `ICfg` erneut lesen,
wenn du den zuletzt veröffentlichten Zustand beobachten möchtest.

Wenn eine eingebaute Quelle `versionStampFactory` verwendet, etabliert die erste erfolgreich abgeschlossene Materialisierung eine akzeptierte authoritative Stamp-Basis.
Jede spätere erfolgreich abgeschlossene Rematerialisierung aktualisiert diese Basis, selbst wenn die aktuelle Snapshot-Instanz beibehalten wird, weil der materialisierte Inhalt unverändert ist.
Spätere gleiche Stamps, einschließlich wiederholtem `null`, überspringen Reread-, Reparse- oder Re-Enumeration-Arbeit.
Ein geänderter Stamp erzwingt eine Rematerialisierung, aber der aktuelle Snapshot kann dennoch beibehalten werden, wenn der materialisierte Inhalt unverändert bleibt.

Die eingebaute Komposition behält die normale Priorität bei: spätere Quellen überschreiben frühere. PicoCfg kann Lesezugriffe im eingeschwungenen Zustand intern optimieren, ohne das exakte Schlüsselverhalten zu ändern, das über `ICfg` und `ICfgRoot` sichtbar ist.

## Benutzerdefinierte Quellen

`PicoCfg.Abs` konzentriert sich jetzt auf die minimalen Consumer-Verträge. Niedrigere Hooks für Quellen- und Provider-Komposition sind Implementierungsdetails und nicht Teil der primären anwendungsseitigen API.

## Erweiterte Anpassung

Die primäre öffentliche API bleibt bewusst klein: `ICfg`, `ICfgRoot`, `CfgBind` und die darauf aufbauenden DI-Helfer. Niedrigere Builder- und Kompositions-Hooks bleiben intern.

## Native AOT

PicoCfg wurde so entwickelt, dass es für Native-AOT-Szenarien geeignet bleibt.
Das Repository enthält `samples/PicoCfg.Sample` sowie eine CI-Validierung mit `dotnet publish -p:PublishAOT=true`.

Beispiel:

```bash
dotnet publish samples/PicoCfg.Sample \
  -c Release \
  -r win-x64 \
  -p:PublishAOT=true \
  --self-contained
```

Passe die Runtime-Identifier an deine Zielplattform an.

## Erstellen und Testen

Diese Befehle entsprechen dem CI-Workflow des Repositorys:

```bash
dotnet restore tests/PicoCfg.Tests/PicoCfg.Tests.csproj -p:UseProjectReferences=true
dotnet build tests/PicoCfg.Tests/PicoCfg.Tests.csproj --configuration Release --no-restore -p:UseProjectReferences=true
dotnet test --project tests/PicoCfg.Tests/PicoCfg.Tests.csproj --configuration Release --no-build --verbosity normal -p:UseProjectReferences=true
dotnet test --project tests/PicoCfg.DI.Tests/PicoCfg.DI.Tests.csproj --configuration Release --no-build --verbosity normal -p:UseProjectReferences=true
```

Das Sample lokal ausführen:

```bash
dotnet run --project samples/PicoCfg.Sample/PicoCfg.Sample.csproj
dotnet run --project samples/PicoCfg.Gen.Sample/PicoCfg.Gen.Sample.csproj
dotnet run --project samples/PicoCfg.DI.Sample/PicoCfg.DI.Sample.csproj
```

## Lizenz

MIT License.
