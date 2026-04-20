# PicoCfg

[English](README.md) | [简体中文](README.zh.md) | [繁體中文](README.zh-TW.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Français](README.fr.md) | [日本語](README.ja.md) | [Português (Brasil)](README.pt-BR.md) | [Русский](README.ru.md)

[![CI](https://github.com/PicoHex/PicoCfg/actions/workflows/ci.yml/badge.svg)](https://github.com/PicoHex/PicoCfg/actions/workflows/ci.yml)

PicoCfg 是一個面向 .NET 的小型、AOT 友善設定函式庫。
它可以將多個來源組合成穩定的唯讀 snapshot，支援顯式 reload，並為已發布的更新提供 one-shot change signal。

## 為什麼選擇 PicoCfg

- 小型公共介面
- 精確字串鍵查找
- 顯式 reload 與 change-signal 語義
- 透過 `PicoCfg.Abs` 提供最小化消費契約
- 面向 Native AOT 的設計

## 安裝

大多數應用只需要 `PicoCfg`：

```bash
dotnet add package PicoCfg
```

如果你只需要最小化消費契約，例如 `ICfg` 和 `ICfgRoot`，請使用 `PicoCfg.Abs`：

```bash
dotnet add package PicoCfg.Abs
```

如果你想以 AOT 友善的方式透過原始碼產生把 PicoCfg 的設定視圖綁定到平坦 POCO，請使用 `PicoCfg.Gen`。此套件提供產生器，而 `CfgBind` 執行期 API 位於 `PicoCfg` 中：

```bash
dotnet add package PicoCfg.Gen
```

如果你想使用 `PicoCfg.DI` 為 `ICfgRoot`、`ICfg` 與以生成綁定為基礎的設定服務提供 PicoDI 註冊輔助，請使用 `PicoCfg.DI`。如果你在消費端應用中需要 `SvcContainer` 這類執行期容器實作，請另外安裝 `PicoDI`。在 project-reference 模式下，請在使用端應用中保留對 `PicoCfg.Gen` 的直接參考，這樣 binder generator 才會為你的 `RegisterCfg*<T>` 呼叫執行：

```bash
dotnet add package PicoCfg.DI
dotnet add package PicoCfg.Gen
dotnet add package PicoDI
```

## 快速開始

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

後加入的 source 會覆蓋先加入的 source。

## 使用 PicoCfg.Gen 的生成綁定

`PicoCfg.Gen` 為 PicoCfg 的精確鍵模型提供 source generator，而 `PicoCfg` 提供生成 binder 所使用的 `CfgBind` 執行期 API。
產生的 binder 是同步的、trim-friendly，並為 Native AOT 情境而設計。

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

在引用 `PicoCfg.Gen` 之後，可用的生成綁定 API 為：

- `CfgBind.Bind<T>(ICfgRoot, section?)`
- `CfgBind.Bind<T>(ICfg, section?)`
- `CfgBind.TryBind<T>(...)`
- `CfgBind.BindInto<T>(...)`

### PicoCfg.Gen v1 範圍

目前生成綁定器刻意保持在較小範圍：

- 僅支援 direct closed generic `CfgBind` 呼叫
- 僅支援 concrete class target
- 僅支援 flat public writable scalar property
- 屬性名稱採大小寫敏感的精確比對
- 在 PicoCfg 精確 key 之上支援可選 `section:` 前綴拼接

以下情況會產生建置診斷，而不是回退到執行期反射：

- 巢狀或複雜物件屬性
- 集合屬性
- open generic target
- 不支援的屬性型別

`Bind<T>` 與 `TryBind<T>` 需要 public parameterless constructor。
如果你要把值寫入現有實例，`BindInto<T>` 仍可用於沒有該建構函式的型別。

儲存庫也包含 `samples/PicoCfg.Gen.Sample`，可作為完整生成綁定範例。

## PicoCfg.DI 與 PicoDI

`PicoCfg.DI` 在 `PicoCfg` 與 `PicoCfg.Gen` 之上加入了對 PicoDI 友善的註冊輔助。
當你已經有 `ICfgRoot` 時使用 `RegisterCfgRoot(...)`，當你想透過 PicoDI 解析以生成綁定為基礎的 POCO 時使用 `RegisterCfgTransient<T>()` / `RegisterCfgScoped<T>()` / `RegisterCfgSingleton<T>()`。

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

儲存庫也包含 `samples/PicoCfg.DI.Sample`，可作為完整 PicoDI 整合範例。

## 核心語義

### 精確鍵查找

`GetValue()` 會對目前 snapshot 執行精確的完整字串查找；如果 key 不存在，則回傳 `null`。
像 `:` 與 `.` 這樣的字元是 key 名稱的一部分；PicoCfg 不會將它們解讀為階層式巡覽。

### 已發布的設定視圖 [advanced]

`ICfgRoot` 會一直從目前已發布的組合設定視圖讀取。
如果 reload 沒有發布新的視圖，讀取仍會持續觀察相同的已發布狀態。
Root 的發布依據是組合後的 provider snapshot 序列，而不只是最終合併後的可見值。

大多數應用程式程式碼應持續使用 `ICfg` 進行精確查找，使用 `ICfgRoot` 處理擁有權、reload 與等待語義，並使用已綁定的 POCO 做型別化消費。

### 生命週期

建構出的 root 擁有已開啟的 provider，並實作 `IAsyncDisposable`。
正常使用時建議搭配 `await using`。
Dispose 會釋放其擁有的 provider，但不會使已經取得的 snapshot 或與特定版本綁定的 change signal 失效。

## 來源類型

### 字串來源

```csharp
builder.Add("Key1=Value1\nKey2=Value2");
```

會以逐行 `key=value` 文字格式進行解析。

### 字典來源

```csharp
builder.Add(new Dictionary<string, string>
{
    ["RawValue"] = "a=b=c",
    ["MultiLine"] = "line1\nline2",
});
```

Dictionary 中的值會直接使用原值。
它們不會再被重新解析為文字，因此內嵌的 `=` 字元與換行內容都會被保留。

### 串流來源

```csharp
builder.Add(() => File.OpenRead("app.cfg"));
```

每次 reload 時都會用與 string content 相同的逐行 `key=value` 格式重新解析。

### 文字與串流解析規則

對於 text 與 stream source：

- 會忽略空白行
- 會忽略不含 `=` 的 malformed line
- 只有第一個 `=` 會用來分隔 key 與 value
- key 與 value 都會被 trim

例如，`Key = a=b=c` 會產生 key `Key` 和 value `a=b=c`。

## 重新載入與變更訊號

- `ReloadAsync()` 只有在發布新 snapshot 實例時才會回傳 `true`
- `ReloadAsync()` 在保留目前 snapshot 實例時回傳 `false`
- 每次 `ReloadAsync()` 呼叫最多只會發布一個新的組合 snapshot
- `WaitForChangeAsync()` 是提供給 root 使用方的公共變更通知原語

如果 reload 在某些 provider 已經發布新 snapshot 版本之後發生例外或被取消，
root 可能會在 reload task 全部 settle 之後，先發布這些已 settle provider version 所觀察到的組合 snapshot，
然後再重新拋出失敗。當 reload 失敗之後，如果你需要觀察最新已發布狀態，應透過 `ICfgRoot` 或 `ICfg` 重新讀取。

當內建 source 使用 `versionStampFactory` 時，第一次成功完成的 materialization 會建立一個已接受的 authoritative stamp baseline。
之後任何成功完成的 rematerialization，即使因為 materialized content 未改變而保留目前 snapshot 實例，也會推進這個 baseline。
之後相等的 stamp（包含重複的 `null`）會跳過 reread、reparse 或 re-enumeration 工作。
當 stamp 改變時會強制 rematerialization，但若 materialized content 未改變，目前 snapshot 仍可能被保留。

內建組合會保留正常優先序，後加入的 source 會覆蓋先加入的 source。PicoCfg 可能會在內部最佳化穩定狀態下的讀取，但不會改變透過 `ICfg` 和 `ICfgRoot` 暴露的精確鍵行為。

## 自訂來源

`PicoCfg.Abs` 現在聚焦於最小化的消費端契約。更低層的 source 與 provider 組合 hooks 屬於實作細節，不是主要的應用程式 API。

## 進階自訂

主要公共 API 刻意保持精簡，只包含 `ICfg`、`ICfgRoot`、`CfgBind`，以及建立在它們之上的 DI 輔助。更低層的 builder 與組合 hooks 會維持為內部實作。

## Native AOT

PicoCfg 的設計目標是保持對 Native AOT 情境友善。
儲存庫包含 `samples/PicoCfg.Sample`，並在 CI 中使用 `dotnet publish -p:PublishAOT=true` 驗證。

範例：

```bash
dotnet publish samples/PicoCfg.Sample \
  -c Release \
  -r win-x64 \
  -p:PublishAOT=true \
  --self-contained
```

請依你的目標平台調整 runtime identifier。

## 建置與測試

以下命令與儲存庫 CI workflow 一致：

```bash
dotnet restore tests/PicoCfg.Tests/PicoCfg.Tests.csproj -p:UseProjectReferences=true
dotnet build tests/PicoCfg.Tests/PicoCfg.Tests.csproj --configuration Release --no-restore -p:UseProjectReferences=true
dotnet test --project tests/PicoCfg.Tests/PicoCfg.Tests.csproj --configuration Release --no-build --verbosity normal -p:UseProjectReferences=true
dotnet test --project tests/PicoCfg.DI.Tests/PicoCfg.DI.Tests.csproj --configuration Release --no-build --verbosity normal -p:UseProjectReferences=true
```

在本機執行 sample：

```bash
dotnet run --project samples/PicoCfg.Sample/PicoCfg.Sample.csproj
dotnet run --project samples/PicoCfg.Gen.Sample/PicoCfg.Gen.Sample.csproj
dotnet run --project samples/PicoCfg.DI.Sample/PicoCfg.DI.Sample.csproj
```

## 授權

MIT License.
