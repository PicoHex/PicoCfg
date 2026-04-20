# PicoCfg

[English](README.md) | [简体中文](README.zh.md) | [繁體中文](README.zh-TW.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Français](README.fr.md) | [日本語](README.ja.md) | [Português (Brasil)](README.pt-BR.md) | [Русский](README.ru.md)

[![CI](https://github.com/PicoHex/PicoCfg/actions/workflows/ci.yml/badge.svg)](https://github.com/PicoHex/PicoCfg/actions/workflows/ci.yml)

PicoCfg は、.NET 向けの小さく AOT フレンドリーな設定ライブラリです。
複数のソースを安定した読み取り専用 snapshot に合成し、明示的な reload をサポートし、公開済み更新に対する one-shot change signal を提供します。

## PicoCfg を選ぶ理由

- 小さな public surface
- 正確な string key lookup
- 明示的な reload / change-signal セマンティクス
- `PicoCfg.Abs` による最小限の consumer 向け契約
- Native AOT フレンドリーな設計

## インストール

ほとんどのアプリケーションでは `PicoCfg` だけで十分です。

```bash
dotnet add package PicoCfg
```

`ICfg` や `ICfgRoot` のような最小限の consumer 向け契約だけが必要な場合は `PicoCfg.Abs` を使用します。

```bash
dotnet add package PicoCfg.Abs
```

PicoCfg の構成ビューを AOT フレンドリーに source generation でフラットな POCO へバインドしたい場合は `PicoCfg.Gen` を使用します。このパッケージは generator を提供し、`CfgBind` のランタイム API は `PicoCfg` にあります。

```bash
dotnet add package PicoCfg.Gen
```

`ICfgRoot`、`ICfg`、および生成バインディングを使う構成サービス向けの PicoDI registration helper が必要な場合は `PicoCfg.DI` を使用します。`SvcContainer` のようなランタイムのコンテナー実装が必要な利用側アプリでは、追加で `PicoDI` も参照してください。project-reference モードでは、`RegisterCfg*<T>` 呼び出しに対して binder generator が動作するよう、利用側アプリで `PicoCfg.Gen` への直接参照を維持してください。

```bash
dotnet add package PicoCfg.DI
dotnet add package PicoCfg.Gen
dotnet add package PicoDI
```

## クイックスタート

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

後から追加した source は、先に追加した source を上書きします。

## PicoCfg.Gen による生成バインディング

`PicoCfg.Gen` は PicoCfg の厳密キー モデル向けの source generator を提供し、`PicoCfg` は生成された binder が使う `CfgBind` ランタイム API を提供します。
生成される binder は同期的で trim-friendly であり、Native AOT シナリオを前提に設計されています。

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

`PicoCfg.Gen` を参照すると利用できる生成バインディング API:

- `CfgBind.Bind<T>(ICfgRoot, section?)`
- `CfgBind.Bind<T>(ICfg, section?)`
- `CfgBind.TryBind<T>(...)`
- `CfgBind.BindInto<T>(...)`

### PicoCfg.Gen v1 の対象範囲

現在の生成 binder は意図的に範囲を絞っています。

- direct closed generic な `CfgBind` 呼び出しのみ
- concrete class target のみ
- flat public writable scalar property のみ
- プロパティ名は大文字小文字を区別して厳密一致
- PicoCfg の厳密 key の上で optional な `section:` prefix 合成をサポート

未対応の形はランタイム reflection fallback ではなく build diagnostics として報告されます。たとえば:

- ネストした/複雑なオブジェクト property
- collection property
- open generic target
- 未対応の property type

`Bind<T>` と `TryBind<T>` には public parameterless constructor が必要です。
既存インスタンスへ書き込む場合は、そのコンストラクターがなくても `BindInto<T>` を使えます。

リポジトリには、生成バインディングの小さな end-to-end 例として `samples/PicoCfg.Gen.Sample` も含まれています。

## PicoCfg.DI と PicoDI

`PicoCfg.DI` は `PicoCfg` と `PicoCfg.Gen` の上に PicoDI-friendly な registration helper を追加します。
すでに `ICfgRoot` を持っている場合は `RegisterCfgRoot(...)` を使い、PicoDI 経由で生成バインディング済み POCO を解決したい場合は `RegisterCfgTransient<T>()` / `RegisterCfgScoped<T>()` / `RegisterCfgSingleton<T>()` を使用します。

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

リポジトリには、PicoDI 統合の小さな end-to-end 例として `samples/PicoCfg.DI.Sample` も含まれています。

## コアセマンティクス

### 厳密キー検索

`GetValue()` は現在の snapshot に対して完全一致の full-string lookup を行い、キーが存在しない場合は `null` を返します。
`:` や `.` のような文字はキー名の一部であり、PicoCfg はそれらを階層走査として解釈しません。

### 公開された構成ビュー [advanced]

`ICfgRoot` は常に現在公開されている合成済み構成ビューから読み取ります。
reload が新しいビューを公開しない場合、読み取りは同じ公開状態を引き続き観測します。
Root の公開は、最終的な可視マージ値だけでなく、合成された provider snapshot sequence に従います。

ほとんどのアプリケーションコードでは、厳密な lookup には `ICfg`、ownership、reload、wait セマンティクスには `ICfgRoot`、型付き利用にはバインド済み POCO を使うべきです。

### ライフタイム

構築された root は開かれた provider を所有し、`IAsyncDisposable` を実装します。
通常の使用では `await using` を推奨します。
Dispose は所有している provider を解放しますが、すでに取得した snapshot や version-specific な change signal を無効化しません。

## ソースの種類

### 文字列ソース

```csharp
builder.Add("Key1=Value1\nKey2=Value2");
```

行ベースの `key=value` テキストとして解析されます。

### 辞書ソース

```csharp
builder.Add(new Dictionary<string, string>
{
    ["RawValue"] = "a=b=c",
    ["MultiLine"] = "line1\nline2",
});
```

Dictionary の値はそのまま使用されます。
再度テキストとして解析されないため、埋め込まれた `=` や改行を含む内容も保持されます。

### ストリームソース

```csharp
builder.Add(() => File.OpenRead("app.cfg"));
```

string content と同じ行ベースの `key=value` 形式で、reload のたびに再解析されます。

### テキストとストリームの解析ルール

text source と stream source では次のルールが適用されます。

- 空行は無視されます
- `=` を含まない malformed line は無視されます
- 最初の `=` だけが key と value を分割します
- key と value は trim されます

たとえば `Key = a=b=c` は、key `Key` と value `a=b=c` を生成します。

## リロードと変更シグナル

- `ReloadAsync()` は新しい snapshot インスタンスが公開されたときだけ `true` を返します
- `ReloadAsync()` は現在の snapshot インスタンスが保持された場合に `false` を返します
- 各 `ReloadAsync()` 呼び出しで公開される新しい composed snapshot は最大 1 つです
- `WaitForChangeAsync()` は root consumer 向けの公開 change notification primitive です

一部の provider がすでに新しい snapshot version を公開した後に reload が例外を投げるかキャンセルされた場合、
root は reload task が settle した後、その settle 済み provider version に対して観測された composed snapshot を先に公開し、
その後で failure を再送出することがあります。失敗した reload の後に最新の公開状態を観測したい場合は、`ICfgRoot` または `ICfg` を通して再読み取りしてください。

組み込み source が `versionStampFactory` を使用する場合、最初に完了した materialization によって accepted authoritative stamp baseline が確立されます。
その後に完了した rematerialization は、materialized content が変わらず現在の snapshot インスタンスが保持される場合でも、この baseline を更新します。
後続の同一 stamp（繰り返しの `null` を含む）は reread、reparse、re-enumeration の作業をスキップします。
stamp が変化すると rematerialization が強制されますが、materialized content が変わらなければ現在の snapshot は保持されることがあります。

組み込みの合成では通常の precedence が維持され、後から追加した source が先の source を上書きします。PicoCfg は `ICfg` と `ICfgRoot` を通して公開される厳密キー動作を変えずに、定常時の読み取りを内部的に最適化する場合があります。

## カスタムソース

`PicoCfg.Abs` は、consumer 向けの最小契約に集中しています。より低レベルな source や provider の composition hook は実装詳細であり、主要なアプリ向け API には含まれません。

## 高度なカスタマイズ

主要な public API は意図的に小さく保たれています。`ICfg`、`ICfgRoot`、`CfgBind`、そしてそれらの上に構築された DI helper です。より低レベルな builder と composition hook は internal のままです。

## Native AOT

PicoCfg は Native AOT シナリオにフレンドリーであり続けるよう設計されています。
リポジトリには `samples/PicoCfg.Sample` が含まれており、CI では `dotnet publish -p:PublishAOT=true` を使って検証しています。

例:

```bash
dotnet publish samples/PicoCfg.Sample \
  -c Release \
  -r win-x64 \
  -p:PublishAOT=true \
  --self-contained
```

対象プラットフォームに合わせて runtime identifier を調整してください。

## ビルドとテスト

これらのコマンドはリポジトリの CI workflow と一致しています。

```bash
dotnet restore tests/PicoCfg.Tests/PicoCfg.Tests.csproj -p:UseProjectReferences=true
dotnet build tests/PicoCfg.Tests/PicoCfg.Tests.csproj --configuration Release --no-restore -p:UseProjectReferences=true
dotnet test --project tests/PicoCfg.Tests/PicoCfg.Tests.csproj --configuration Release --no-build --verbosity normal -p:UseProjectReferences=true
dotnet test --project tests/PicoCfg.DI.Tests/PicoCfg.DI.Tests.csproj --configuration Release --no-build --verbosity normal -p:UseProjectReferences=true
```

sample をローカルで実行:

```bash
dotnet run --project samples/PicoCfg.Sample/PicoCfg.Sample.csproj
dotnet run --project samples/PicoCfg.Gen.Sample/PicoCfg.Gen.Sample.csproj
dotnet run --project samples/PicoCfg.DI.Sample/PicoCfg.DI.Sample.csproj
```

## ライセンス

MIT License.
