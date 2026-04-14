# PicoCfg

[English](README.md) | [简体中文](README.zh.md) | [繁體中文](README.zh-TW.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Français](README.fr.md) | [日本語](README.ja.md) | [Português (Brasil)](README.pt-BR.md) | [Русский](README.ru.md)

[![CI](https://github.com/PicoHex/PicoCfg/actions/workflows/ci.yml/badge.svg)](https://github.com/PicoHex/PicoCfg/actions/workflows/ci.yml)

PicoCfg 是一個面向 .NET 的小型、AOT 友善設定函式庫。
它可以將多個來源組合成穩定的唯讀 snapshot，支援顯式 reload，並為已發布的更新提供 one-shot change signal。

## 為什麼選擇 PicoCfg

- 小型公共介面
- 精確字串鍵查找
- 顯式 reload 與 change-signal 語義
- 透過 `PicoCfg.Abs` 支援自訂 source
- 面向 Native AOT 的設計

## 安裝

大多數應用只需要 `PicoCfg`：

```bash
dotnet add package PicoCfg
```

如果你只需要用於自訂整合或抽象的契約，請使用 `PicoCfg.Abs`：

```bash
dotnet add package PicoCfg.Abs
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

Console.WriteLine(root.Snapshot.GetValue("ConnectionString"));
Console.WriteLine(root.Snapshot.GetValue("Logging:Level"));
```

後加入的 source 會覆蓋先加入的 source。

## 核心語義

### 精確鍵查找

`GetValue()` 會對目前 snapshot 執行精確的完整字串查找；如果 key 不存在，則回傳 `null`。
像 `:` 與 `.` 這樣的字元是 key 名稱的一部分；PicoCfg 不會將它們解讀為階層式巡覽。

### 穩定快照

`ICfgRoot.Snapshot` 會暴露目前已發布的唯讀 snapshot。
如果 reload 沒有發布新的 snapshot，就會保留相同的 snapshot 實例。
Root 的發布依據是組合後的 provider snapshot 序列，而不只是最終合併後的可見值。
在 root dispose 之後，已經取得的 snapshot 仍然可用。

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
- `GetChangeSignal()` 會回傳目前已發布版本對應的 one-shot signal
- 每次 `ReloadAsync()` 呼叫最多只會發布一個新的組合 snapshot
- 發布變更之後，如果要等待之後的變更，請重新取得新的 signal，因為 signal 只對應單一已發布版本

如果 reload 在某些 provider 已經發布新 snapshot 版本之後發生例外或被取消，
root 可能會在 reload task 全部 settle 之後，先發布這些已 settle provider version 所觀察到的組合 snapshot，
然後再重新拋出失敗。當 reload 失敗之後，如果你需要觀察最新已發布狀態，應重新讀取 `Snapshot` 並取得新的 change signal。

當內建 source 使用 `versionStampFactory` 時，第一次成功完成的 materialization 會建立一個已接受的 authoritative stamp baseline。
之後任何成功完成的 rematerialization，即使因為 materialized content 未改變而保留目前 snapshot 實例，也會推進這個 baseline。
之後相等的 stamp（包含重複的 `null`）會跳過 reread、reparse 或 re-enumeration 工作。
當 stamp 改變時會強制 rematerialization，但若 materialized content 未改變，目前 snapshot 仍可能被保留。

當所有組合後的 provider snapshot 都是 PicoCfg 原生 snapshot 類型時，root 會將它們扁平化為單一 dictionary-backed snapshot，以優化 steady-state 讀取。
如果任一 provider 提供的是自訂 `ICfgSnapshot`，root 會保留該自訂 lookup 行為，並回退到 read-time provider scanning，而不是將這些自訂語義扁平化掉。
即使在 fallback 組合模式下，仍然遵守正常優先序：後面的 provider 會覆蓋前面的 provider。

## 自訂來源

自訂整合是建立在 `PicoCfg.Abs` 之上。

- `ICfgSource.OpenAsync()` 會將一個 source 開啟為長生命週期的 provider
- 回傳的 provider 必須已經暴露可讀的 `Snapshot`
- `ICfgProvider.ReloadAsync()` 會回報該 provider 是否發布了新的 snapshot 實例；當回傳 `false` 時，表示該 provider version authoritative unchanged，呼叫端可以保留目前的 snapshot 參考
- `ICfgProvider.GetChangeSignal()` 會回傳目前已發布版本對應的 one-shot signal

最小範例：

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
```

在本機執行 sample：

```bash
dotnet run --project samples/PicoCfg.Sample/PicoCfg.Sample.csproj
```

## 授權

MIT License.
