# PicoCfg

[English](README.md) | [简体中文](README.zh.md) | [繁體中文](README.zh-TW.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Français](README.fr.md) | [日本語](README.ja.md) | [Português (Brasil)](README.pt-BR.md) | [Русский](README.ru.md)

[![CI](https://github.com/PicoHex/PicoCfg/actions/workflows/ci.yml/badge.svg)](https://github.com/PicoHex/PicoCfg/actions/workflows/ci.yml)

PicoCfg est une bibliothèque de configuration petite et compatible AOT pour .NET.
Elle compose plusieurs sources en un snapshot stable en lecture seule, prend en charge un reload explicite et expose un one-shot change signal pour les mises à jour publiées.

## Pourquoi PicoCfg

- surface publique réduite
- recherche exacte par clé string
- sémantique explicite de reload et de change signal
- contrats consommateurs minimaux via `PicoCfg.Abs`
- conception compatible avec Native AOT

## Installation

La plupart des applications n'ont besoin que de `PicoCfg` :

```bash
dotnet add package PicoCfg
```

Utilisez `PicoCfg.Abs` si vous n'avez besoin que des contrats consommateurs minimaux comme `ICfg` et `ICfgRoot` :

```bash
dotnet add package PicoCfg.Abs
```

Utilisez `PicoCfg.Gen` si vous souhaitez lier des vues de configuration PicoCfg à des POCO plats de manière compatible AOT via la génération de source. Le package apporte le générateur, tandis que l'API runtime `CfgBind` se trouve dans `PicoCfg` :

```bash
dotnet add package PicoCfg.Gen
```

Utilisez `PicoCfg.DI` si vous voulez des helpers d'enregistrement compatibles avec PicoDI pour `ICfgRoot`, `ICfg` et des services de configuration adossés au binding généré. Ajoutez également `PicoDI` dans l'application consommatrice lorsque vous avez besoin de l'implémentation runtime du conteneur, comme `SvcContainer`. En mode project-reference, conservez une référence directe à `PicoCfg.Gen` dans l'application consommatrice afin que le générateur de binder s'exécute pour vos appels `RegisterCfg*<T>` :

```bash
dotnet add package PicoCfg.DI
dotnet add package PicoCfg.Gen
dotnet add package PicoDI
```

## Démarrage rapide

```csharp
using System.Text;
using PicoCfg;
using PicoCfg.Abs;
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

Les sources ajoutées plus tard remplacent celles ajoutées plus tôt.

## Liaison générée avec PicoCfg.Gen

`PicoCfg.Gen` apporte le générateur de source pour le modèle à clés exactes de PicoCfg, tandis que `PicoCfg` fournit l'API runtime `CfgBind` utilisée par le binder généré.
Le binder généré est synchrone, compatible trim et conçu pour les scénarios Native AOT.

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

Avec `PicoCfg.Gen` référencé, la surface de liaison générée disponible est :

- `CfgBind.Bind<T>(ICfgRoot, section?)`
- `CfgBind.Bind<T>(ICfg, section?)`
- `CfgBind.TryBind<T>(...)`
- `CfgBind.BindInto<T>(...)`

### Périmètre de PicoCfg.Gen v1

Le binder généré actuel reste volontairement limité :

- uniquement des appels `CfgBind` génériques fermés directs
- uniquement des cibles de type classe concrète
- uniquement des propriétés scalaires publiques et modifiables dans une structure plate
- correspondance exacte et sensible à la casse des noms de propriété
- composition optionnelle du préfixe `section:` au-dessus des clés exactes de PicoCfg

Les formes non prises en charge produisent des diagnostics de build au lieu d'un fallback par réflexion à l'exécution, notamment :

- propriétés d'objet imbriquées ou complexes
- propriétés de collection
- cibles open generic
- types de propriété non pris en charge

`Bind<T>` et `TryBind<T>` nécessitent un constructeur public sans paramètre.
`BindInto<T>` peut toujours écrire dans une instance existante qui n'en possède pas.

Le dépôt inclut aussi `samples/PicoCfg.Gen.Sample` comme petit exemple de liaison générée end-to-end.

## PicoCfg.DI avec PicoDI

`PicoCfg.DI` ajoute des helpers d'enregistrement compatibles avec PicoDI au-dessus de `PicoCfg` et `PicoCfg.Gen`.
Utilisez `RegisterCfgRoot(...)` si vous possédez déjà un `ICfgRoot`, et `RegisterCfgTransient<T>()` / `RegisterCfgScoped<T>()` / `RegisterCfgSingleton<T>()` si vous voulez résoudre via PicoDI des POCO adossés au binding généré.

```csharp
using PicoCfg;
using PicoCfg.Abs;
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

Le dépôt inclut aussi `samples/PicoCfg.DI.Sample` comme petit exemple end-to-end d'intégration PicoDI.

## Sémantique principale

### Recherche exacte par clé

`GetValue()` effectue une recherche exacte sur la chaîne complète dans le snapshot courant et renvoie `null` si la clé est absente.
Les caractères comme `:` et `.` font partie du nom de clé ; PicoCfg ne les interprète pas comme une traversée hiérarchique.

### Vues de configuration publiées [advanced]

`ICfgRoot` lit toujours depuis la vue de configuration composée actuellement publiée.
Si un reload ne publie pas de nouvelle vue, les lectures continuent d'observer le même état publié.
La publication du root suit la séquence composée des provider snapshots, et pas uniquement les valeurs visibles finales fusionnées.

La plupart du code applicatif devrait rester sur `ICfg` pour les recherches exactes, `ICfgRoot` pour la sémantique de possession, de reload et d'attente, et des POCO liés pour une consommation typée.

### Cycle de vie

Le root construit possède les providers ouverts et implémente `IAsyncDisposable`.
Préférez `await using` pour l'usage normal.
Le dispose libère les providers possédés, mais n'invalide pas les snapshots ni les change signals spécifiques à une version déjà obtenus.

## Types de source

### Source chaîne

```csharp
builder.Add("Key1=Value1\nKey2=Value2");
```

Analysé comme un contenu texte `key=value` basé sur des lignes.

### Source dictionnaire

```csharp
builder.Add(new Dictionary<string, string>
{
    ["RawValue"] = "a=b=c",
    ["MultiLine"] = "line1\nline2",
});
```

Les valeurs du dictionnaire sont utilisées telles quelles.
Elles ne sont pas ré-analysées comme du texte, ce qui préserve les caractères `=` intégrés et le contenu multiligne.

### Source flux

```csharp
builder.Add(() => File.OpenRead("app.cfg"));
```

Ré-analysé à chaque reload en utilisant le même format `key=value` basé sur des lignes que le contenu string.

### Règles d'analyse du texte et des flux

Pour les sources texte et stream :

- les lignes vides sont ignorées
- les lignes mal formées sans `=` sont ignorées
- seul le premier `=` sépare la clé et la valeur
- les clés et les valeurs sont trimées

Par exemple, `Key = a=b=c` produit la clé `Key` et la valeur `a=b=c`.

## Rechargement et signaux de changement

- `ReloadAsync()` renvoie `true` uniquement lorsqu'une nouvelle instance de snapshot est publiée
- `ReloadAsync()` renvoie `false` lorsque l'instance actuelle du snapshot est conservée
- chaque appel à `ReloadAsync()` publie au plus un nouveau snapshot composé
- `WaitForChangeAsync()` est la primitive publique de notification de changement pour les consommateurs de root

Si un reload lève une exception ou est annulé après que certains providers ont déjà publié de nouvelles versions de snapshot,
le root peut d'abord publier le snapshot composé observé pour ces versions de provider déjà stabilisées, une fois les tâches de reload stabilisées,
puis relancer l'échec. Après un reload échoué, relisez via `ICfgRoot` ou `ICfg` si vous devez observer l'état publié le plus récent.

Lorsqu'une source intégrée utilise `versionStampFactory`, la première matérialisation terminée établit une baseline de stamp authoritative acceptée.
Toute rematérialisation terminée ultérieurement met à jour cette baseline, même lorsque l'instance actuelle du snapshot est conservée parce que le contenu matérialisé n'a pas changé.
Les stamps identiques ultérieurs, y compris les `null` répétés, évitent le travail de reread, de reparse ou de re-enumeration.
Un stamp modifié force une rematérialisation, mais le snapshot courant peut tout de même être conservé lorsque le contenu matérialisé est inchangé.

La composition intégrée conserve la priorité normale : les sources ajoutées plus tard remplacent les plus anciennes. PicoCfg peut optimiser les lectures stables en interne sans changer le comportement de clé exacte exposé via `ICfg` et `ICfgRoot`.

## Sources personnalisées

`PicoCfg.Abs` reste maintenant centré sur les contrats minimaux orientés consommateur. Les hooks de composition de source et de provider de niveau inférieur sont des détails d'implémentation, pas une partie de l'API principale côté application.

## Personnalisation avancée

L'API publique principale reste volontairement réduite : `ICfg`, `ICfgRoot`, `CfgBind` et les helpers DI construits au-dessus. Les hooks de builder et de composition de niveau inférieur restent internes.

## Native AOT

PicoCfg est conçu pour rester compatible avec les scénarios Native AOT.
Le dépôt inclut `samples/PicoCfg.Sample` ainsi qu'une validation CI utilisant `dotnet publish -p:PublishAOT=true`.

Exemple :

```bash
dotnet publish samples/PicoCfg.Sample \
  -c Release \
  -r win-x64 \
  -p:PublishAOT=true \
  --self-contained
```

Adaptez le runtime identifier à votre plateforme cible.

## Compilation et tests

Ces commandes correspondent au workflow CI du dépôt :

```bash
dotnet restore tests/PicoCfg.Tests/PicoCfg.Tests.csproj -p:UseProjectReferences=true
dotnet build tests/PicoCfg.Tests/PicoCfg.Tests.csproj --configuration Release --no-restore -p:UseProjectReferences=true
dotnet test --project tests/PicoCfg.Tests/PicoCfg.Tests.csproj --configuration Release --no-build --verbosity normal -p:UseProjectReferences=true
dotnet test --project tests/PicoCfg.Gen.Tests/PicoCfg.Gen.Tests.csproj --configuration Release --no-build --verbosity normal -p:UseProjectReferences=true
dotnet test --project tests/PicoCfg.DI.Tests/PicoCfg.DI.Tests.csproj --configuration Release --no-build --verbosity normal -p:UseProjectReferences=true
```

Exécuter l'exemple localement :

```bash
dotnet run --project samples/PicoCfg.Sample/PicoCfg.Sample.csproj
dotnet run --project samples/PicoCfg.Gen.Sample/PicoCfg.Gen.Sample.csproj
dotnet run --project samples/PicoCfg.DI.Sample/PicoCfg.DI.Sample.csproj
```

## Licence

MIT License.
