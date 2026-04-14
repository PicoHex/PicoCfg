# PicoCfg

[English](README.md) | [简体中文](README.zh.md) | [繁體中文](README.zh-TW.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Français](README.fr.md) | [日本語](README.ja.md) | [Português (Brasil)](README.pt-BR.md) | [Русский](README.ru.md)

[![CI](https://github.com/PicoHex/PicoCfg/actions/workflows/ci.yml/badge.svg)](https://github.com/PicoHex/PicoCfg/actions/workflows/ci.yml)

PicoCfg ist eine kleine, AOT-freundliche Konfigurationsbibliothek für .NET.
Sie kombiniert mehrere Quellen zu einem stabilen, schreibgeschützten Snapshot, unterstützt explizites Reload und stellt ein One-Shot-Change-Signal für veröffentlichte Aktualisierungen bereit.

## Warum PicoCfg

- kleine öffentliche Oberfläche
- exakte String-Key-Lookups
- explizite Reload- und Change-Signal-Semantik
- Unterstützung für benutzerdefinierte Quellen über `PicoCfg.Abs`
- Native-AOT-freundliches Design

## Installation

Die meisten Anwendungen benötigen nur `PicoCfg`:

```bash
dotnet add package PicoCfg
```

Verwende `PicoCfg.Abs`, wenn du nur die Verträge für benutzerdefinierte Integrationen oder Abstraktionen benötigst:

```bash
dotnet add package PicoCfg.Abs
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

Console.WriteLine(root.Snapshot.GetValue("ConnectionString"));
Console.WriteLine(root.Snapshot.GetValue("Logging:Level"));
```

Später hinzugefügte Quellen überschreiben früher hinzugefügte Quellen.

## Kernsemantik

### Exakte Schlüsselsuche

`GetValue()` führt ein exaktes Full-String-Lookup auf dem aktuellen Snapshot aus und gibt `null` zurück, wenn der Schlüssel fehlt.
Zeichen wie `:` und `.` sind Teil des Schlüsselnamens; PicoCfg interpretiert sie nicht als hierarchische Traversierung.

### Stabile Snapshots

`ICfgRoot.Snapshot` stellt den aktuell veröffentlichten schreibgeschützten Snapshot bereit.
Wenn ein Reload keinen neuen Snapshot veröffentlicht, bleibt dieselbe Snapshot-Instanz erhalten.
Die Veröffentlichung auf Root-Ebene folgt der zusammengesetzten Provider-Snapshot-Sequenz und nicht nur den final sichtbaren zusammengeführten Werten.
Bereits erhaltene Snapshots bleiben nach dem Dispose des Root weiter verwendbar.

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
- `GetChangeSignal()` gibt das One-Shot-Signal für die aktuell veröffentlichte Version zurück
- jeder `ReloadAsync()`-Aufruf veröffentlicht höchstens einen neuen zusammengesetzten Snapshot
- nach einer veröffentlichten Änderung muss für spätere Waits ein neues Signal geholt werden, weil Signale an genau eine veröffentlichte Version gebunden sind

Wenn ein Reload eine Ausnahme auslöst oder abgebrochen wird, nachdem einige Provider bereits neue Snapshot-Versionen veröffentlicht haben,
kann der Root nach dem Settlen der Reload-Tasks zuerst den beobachteten zusammengesetzten Snapshot für diese bereits gesettleten Provider-Versionen veröffentlichen
und anschließend den Fehler erneut auslösen. Nach einem fehlgeschlagenen Reload solltest du `Snapshot` erneut lesen und ein neues Change-Signal abrufen,
wenn du den zuletzt veröffentlichten Zustand beobachten möchtest.

Wenn eine eingebaute Quelle `versionStampFactory` verwendet, etabliert die erste erfolgreich abgeschlossene Materialisierung eine akzeptierte authoritative Stamp-Basis.
Jede spätere erfolgreich abgeschlossene Rematerialisierung aktualisiert diese Basis, selbst wenn die aktuelle Snapshot-Instanz beibehalten wird, weil der materialisierte Inhalt unverändert ist.
Spätere gleiche Stamps, einschließlich wiederholtem `null`, überspringen Reread-, Reparse- oder Re-Enumeration-Arbeit.
Ein geänderter Stamp erzwingt eine Rematerialisierung, aber der aktuelle Snapshot kann dennoch beibehalten werden, wenn der materialisierte Inhalt unverändert bleibt.

Wenn alle zusammengesetzten Provider-Snapshots der native Snapshot-Typ von PicoCfg sind, flacht der Root sie in einen einzelnen dictionary-basierten Snapshot für Steady-State-Reads ab.
Wenn irgendein Provider ein benutzerdefiniertes `ICfgSnapshot` liefert, behält der Root dieses benutzerdefinierte Lookup-Verhalten bei und fällt auf Read-Time-Provider-Scans zurück,
anstatt die benutzerdefinierte Semantik wegzuoptimieren. Der Fallback respektiert weiterhin die normale Priorität: spätere Provider überschreiben frühere.

## Benutzerdefinierte Quellen

Benutzerdefinierte Integrationen basieren auf `PicoCfg.Abs`.

- `ICfgSource.OpenAsync()` öffnet eine Quelle in einen langlebigen Provider
- der zurückgegebene Provider muss bereits einen lesbaren `Snapshot` bereitstellen
- `ICfgProvider.ReloadAsync()` meldet, ob dieser Provider eine neue Snapshot-Instanz veröffentlicht hat; `false` bedeutet authoritative unchanged für diese Provider-Version, und Aufrufer dürfen die aktuelle Snapshot-Referenz behalten
- `ICfgProvider.GetChangeSignal()` gibt das One-Shot-Signal für die aktuell veröffentlichte Version zurück

Minimales Beispiel:

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
```

Das Sample lokal ausführen:

```bash
dotnet run --project samples/PicoCfg.Sample/PicoCfg.Sample.csproj
```

## Lizenz

MIT License.
