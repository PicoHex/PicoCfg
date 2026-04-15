# PicoCfg

[English](README.md) | [简体中文](README.zh.md) | [繁體中文](README.zh-TW.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Français](README.fr.md) | [日本語](README.ja.md) | [Português (Brasil)](README.pt-BR.md) | [Русский](README.ru.md)

[![CI](https://github.com/PicoHex/PicoCfg/actions/workflows/ci.yml/badge.svg)](https://github.com/PicoHex/PicoCfg/actions/workflows/ci.yml)

PicoCfg は、.NET 向けの小さく AOT フレンドリーな設定ライブラリです。
複数のソースを安定した読み取り専用 snapshot に合成し、明示的な reload をサポートし、公開済み更新に対する one-shot change signal を提供します。

## PicoCfg を選ぶ理由

- 小さな public surface
- 正確な string key lookup
- 明示的な reload / change-signal セマンティクス
- `PicoCfg.Abs` による custom source サポート
- Native AOT フレンドリーな設計

## インストール

ほとんどのアプリケーションでは `PicoCfg` だけで十分です。

```bash
dotnet add package PicoCfg
```

カスタム統合や抽象化のために契約だけが必要な場合は `PicoCfg.Abs` を使用します。

```bash
dotnet add package PicoCfg.Abs
```

PicoCfg の snapshot や root を AOT フレンドリーにフラットな POCO へバインドしたい場合は `PicoCfg.Gen` を使用します。

```bash
dotnet add package PicoCfg.Gen
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

Console.WriteLine(root.Snapshot.GetValue("ConnectionString"));
Console.WriteLine(root.Snapshot.GetValue("Logging:Level"));
```

後から追加した source は、先に追加した source を上書きします。

## PicoCfg.Gen による生成バインディング

`PicoCfg.Gen` は、PicoCfg の厳密 key/snapshot モデルの上に source-generated なバインディング補助を追加します。
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

現在の `PicoCfg.Gen` が提供する API:

- `PicoCfgBind.Bind<T>(ICfgSnapshot, section?)`
- `PicoCfgBind.Bind<T>(ICfgRoot, section?)`
- `PicoCfgBind.TryBind<T>(...)`
- `PicoCfgBind.BindInto<T>(...)`

### PicoCfg.Gen v1 の対象範囲

現在の生成 binder は意図的に範囲を絞っています。

- direct closed generic な `PicoCfgBind` 呼び出しのみ
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

## コアセマンティクス

### 厳密キー検索

`GetValue()` は現在の snapshot に対して完全一致の full-string lookup を行い、キーが存在しない場合は `null` を返します。
`:` や `.` のような文字はキー名の一部であり、PicoCfg はそれらを階層走査として解釈しません。

### 安定したスナップショット

`ICfgRoot.Snapshot` は現在公開されている読み取り専用 snapshot を公開します。
reload が新しい snapshot を公開しない場合、同じ snapshot インスタンスが保持されます。
Root の公開は、最終的な可視マージ値だけでなく、合成された provider snapshot sequence に従います。
すでに取得済みの snapshot は root を dispose した後も利用可能です。

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
- `GetChangeSignal()` は現在公開されている version に対応する one-shot signal を返します
- 各 `ReloadAsync()` 呼び出しで公開される新しい composed snapshot は最大 1 つです
- 公開済み変更の後で次の変更を待つには、新しい signal を取得してください。signal は単一の公開 version に結び付いています

一部の provider がすでに新しい snapshot version を公開した後に reload が例外を投げるかキャンセルされた場合、
root は reload task が settle した後、その settle 済み provider version に対して観測された composed snapshot を先に公開し、
その後で failure を再送出することがあります。失敗した reload の後に最新の公開状態を観測したい場合は、`Snapshot` を再取得し、新しい change signal を取得してください。

組み込み source が `versionStampFactory` を使用する場合、最初に完了した materialization によって accepted authoritative stamp baseline が確立されます。
その後に完了した rematerialization は、materialized content が変わらず現在の snapshot インスタンスが保持される場合でも、この baseline を更新します。
後続の同一 stamp（繰り返しの `null` を含む）は reread、reparse、re-enumeration の作業をスキップします。
stamp が変化すると rematerialization が強制されますが、materialized content が変わらなければ現在の snapshot は保持されることがあります。

合成される provider snapshot がすべて PicoCfg のネイティブ snapshot 型である場合、root は steady-state read のためにそれらを単一の dictionary-backed snapshot に flatten します。
いずれかの provider が custom `ICfgSnapshot` を提供する場合、root はその custom lookup behavior を保持し、そのカスタム semantics を flatten して失わないよう read-time provider scanning に fallback します。
fallback composition でも通常の precedence は維持され、後ろの provider が前の provider を上書きします。

## カスタムソース

custom integration は `PicoCfg.Abs` 上に構築されます。

- `ICfgSource.OpenAsync()` は source を長寿命の provider として開きます
- 返される provider は、すでに読み取り可能な `Snapshot` を公開していなければなりません
- `ICfgProvider.ReloadAsync()` はその provider が新しい snapshot インスタンスを公開したかどうかを返します。`false` はその provider version に対して authoritative unchanged であり、呼び出し側は現在の snapshot 参照を保持できます
- `ICfgProvider.GetChangeSignal()` は現在公開されている version に対する one-shot signal を返します

最小スケッチ:

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
```

sample をローカルで実行:

```bash
dotnet run --project samples/PicoCfg.Sample/PicoCfg.Sample.csproj
dotnet run --project samples/PicoCfg.Gen.Sample/PicoCfg.Gen.Sample.csproj
```

## ライセンス

MIT License.
