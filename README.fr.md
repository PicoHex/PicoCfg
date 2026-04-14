# PicoCfg

[English](README.md) | [简体中文](README.zh.md) | [繁體中文](README.zh-TW.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Français](README.fr.md) | [日本語](README.ja.md) | [Português (Brasil)](README.pt-BR.md) | [Русский](README.ru.md)

[![CI](https://github.com/PicoHex/PicoCfg/actions/workflows/ci.yml/badge.svg)](https://github.com/PicoHex/PicoCfg/actions/workflows/ci.yml)

PicoCfg est une bibliothèque de configuration petite et compatible AOT pour .NET.
Elle compose plusieurs sources en un snapshot stable en lecture seule, prend en charge un reload explicite et expose un one-shot change signal pour les mises à jour publiées.

## Pourquoi PicoCfg

- surface publique réduite
- recherche exacte par clé string
- sémantique explicite de reload et de change signal
- prise en charge des sources personnalisées via `PicoCfg.Abs`
- conception compatible avec Native AOT

## Installation

La plupart des applications n'ont besoin que de `PicoCfg` :

```bash
dotnet add package PicoCfg
```

Utilisez `PicoCfg.Abs` si vous n'avez besoin que des contrats pour des intégrations ou abstractions personnalisées :

```bash
dotnet add package PicoCfg.Abs
```

## Démarrage rapide

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

Les sources ajoutées plus tard remplacent celles ajoutées plus tôt.

## Sémantique principale

### Recherche exacte par clé

`GetValue()` effectue une recherche exacte sur la chaîne complète dans le snapshot courant et renvoie `null` si la clé est absente.
Les caractères comme `:` et `.` font partie du nom de clé ; PicoCfg ne les interprète pas comme une traversée hiérarchique.

### Snapshots stables

`ICfgRoot.Snapshot` expose le snapshot actuellement publié en lecture seule.
Si un reload ne publie pas de nouveau snapshot, la même instance de snapshot est conservée.
La publication du root suit la séquence composée des provider snapshots, et pas uniquement les valeurs visibles finales fusionnées.
Les snapshots déjà obtenus restent utilisables après le dispose du root.

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
- `GetChangeSignal()` renvoie le one-shot signal pour la version actuellement publiée
- chaque appel à `ReloadAsync()` publie au plus un nouveau snapshot composé
- après un changement publié, récupérez un nouveau signal pour les attentes ultérieures car les signaux sont liés à une seule version publiée

Si un reload lève une exception ou est annulé après que certains providers ont déjà publié de nouvelles versions de snapshot,
le root peut d'abord publier le snapshot composé observé pour ces versions de provider déjà stabilisées, une fois les tâches de reload stabilisées,
puis relancer l'échec. Après un reload échoué, relisez `Snapshot` et récupérez un nouveau change signal si vous devez observer l'état publié le plus récent.

Lorsqu'une source intégrée utilise `versionStampFactory`, la première matérialisation terminée établit une baseline de stamp authoritative acceptée.
Toute rematérialisation terminée ultérieurement met à jour cette baseline, même lorsque l'instance actuelle du snapshot est conservée parce que le contenu matérialisé n'a pas changé.
Les stamps identiques ultérieurs, y compris les `null` répétés, évitent le travail de reread, de reparse ou de re-enumeration.
Un stamp modifié force une rematérialisation, mais le snapshot courant peut tout de même être conservé lorsque le contenu matérialisé est inchangé.

Lorsque tous les provider snapshots composés sont des snapshots natifs de PicoCfg, le root les aplati dans un unique snapshot basé sur un dictionnaire pour les lectures en régime stable.
Si un provider fournit un `ICfgSnapshot` personnalisé, le root préserve ce comportement personnalisé de lookup et retombe sur un scan des providers au moment de la lecture,
au lieu d'aplatir cette sémantique personnalisée. La composition de fallback respecte toujours la priorité normale : les providers plus tardifs remplacent les plus anciens.

## Sources personnalisées

Les intégrations personnalisées sont construites sur `PicoCfg.Abs`.

- `ICfgSource.OpenAsync()` ouvre une source dans un provider de longue durée
- le provider retourné doit déjà exposer un `Snapshot` lisible
- `ICfgProvider.ReloadAsync()` indique si ce provider a publié une nouvelle instance de snapshot ; `false` signifie authoritative unchanged pour cette version du provider, et les appelants peuvent conserver la référence du snapshot courant
- `ICfgProvider.GetChangeSignal()` renvoie le one-shot signal pour la version actuellement publiée

Exemple minimal :

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
```

Exécuter l'exemple localement :

```bash
dotnet run --project samples/PicoCfg.Sample/PicoCfg.Sample.csproj
```

## Licence

MIT License.
