# PicoCfg

[English](README.md) | [简体中文](README.zh.md) | [繁體中文](README.zh-TW.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Français](README.fr.md) | [日本語](README.ja.md) | [Português (Brasil)](README.pt-BR.md) | [Русский](README.ru.md)

[![CI](https://github.com/PicoHex/PicoCfg/actions/workflows/ci.yml/badge.svg)](https://github.com/PicoHex/PicoCfg/actions/workflows/ci.yml)

PicoCfg 是一个面向 .NET 的小型、AOT 友好的配置库。
它可以将多个来源组合成稳定的只读快照，支持显式 reload，并为已发布的更新提供 one-shot change signal。

## 为什么选择 PicoCfg

- 小型公共接口面
- 精确字符串键查找
- 显式 reload 和 change-signal 语义
- 通过 `PicoCfg.Abs` 支持自定义 source
- 面向 Native AOT 的设计

## 安装

大多数应用只需要 `PicoCfg`：

```bash
dotnet add package PicoCfg
```

如果你只需要用于自定义集成或抽象的契约，请使用 `PicoCfg.Abs`：

```bash
dotnet add package PicoCfg.Abs
```

如果你想把 PicoCfg 的 snapshot 或 root 以 AOT 友好的方式通过源码生成绑定到平坦 POCO，请使用 `PicoCfg.Gen`。该包提供生成器，而 `PicoCfgBind` 运行时 API 位于 `PicoCfg` 中：

```bash
dotnet add package PicoCfg.Gen
```

## 快速开始

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

后添加的 source 会覆盖先添加的 source。

## 使用 PicoCfg.Gen 进行生成绑定

`PicoCfg.Gen` 为 PicoCfg 的精确 key snapshot 模型提供源码生成器，而 `PicoCfg` 提供生成绑定器使用的 `PicoCfgBind` 运行时 API。
生成的 binder 是同步的、trim 友好的，并面向 Native AOT 场景设计。

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

在引用 `PicoCfg.Gen` 后，可用的生成绑定 API 为：

- `PicoCfgBind.Bind<T>(ICfgSnapshot, section?)`
- `PicoCfgBind.Bind<T>(ICfgRoot, section?)`
- `PicoCfgBind.TryBind<T>(...)`
- `PicoCfgBind.BindInto<T>(...)`

### PicoCfg.Gen v1 范围

当前生成绑定器刻意保持较小范围：

- 仅支持 direct closed generic `PicoCfgBind` 调用
- 仅支持 concrete class target
- 仅支持 flat public writable scalar property
- 属性名按大小写敏感方式精确匹配
- 在 PicoCfg 精确 key 之上支持可选 `section:` 前缀拼接

以下情况会产生构建诊断，而不是回退到运行时反射：

- 嵌套或复杂对象属性
- 集合属性
- open generic target
- 不支持的属性类型

`Bind<T>` 和 `TryBind<T>` 要求 public parameterless constructor。
如果你要把值写入现有实例，`BindInto<T>` 仍可用于没有该构造函数的类型。

仓库还包含 `samples/PicoCfg.Gen.Sample`，用于展示一个完整的生成绑定示例。

## 核心语义

### 精确键查找

`GetValue()` 会对当前 snapshot 执行精确的完整字符串查找；如果 key 不存在，则返回 `null`。
像 `:` 和 `.` 这样的字符是 key 名称的一部分；PicoCfg 不会把它们解释成层级遍历。

### 稳定快照

`ICfgRoot.Snapshot` 会暴露当前已发布的只读 snapshot。
如果 reload 没有发布新的 snapshot，则保留同一个 snapshot 实例。
Root 的发布依据是组合后的 provider snapshot 序列，而不只是最终合并后的可见值。
在 root dispose 之后，已经获取到的 snapshot 仍然可用。

### 生命周期

构建出来的 root 拥有已打开的 provider，并实现了 `IAsyncDisposable`。
正常使用时推荐配合 `await using`。
Dispose 会释放其拥有的 provider，但不会使已经获取到的 snapshot 或与特定版本绑定的 change signal 失效。

## 来源类型

### 字符串来源

```csharp
builder.Add("Key1=Value1\nKey2=Value2");
```

按基于行的 `key=value` 文本进行解析。

### 字典来源

```csharp
builder.Add(new Dictionary<string, string>
{
    ["RawValue"] = "a=b=c",
    ["MultiLine"] = "line1\nline2",
});
```

Dictionary 中的值会原样使用。
它们不会再次按文本重解析，因此内嵌的 `=` 字符和换行内容都会被保留。

### 流来源

```csharp
builder.Add(() => File.OpenRead("app.cfg"));
```

每次 reload 时都会重新按与 string content 相同的 `key=value` 行格式解析。

### 文本与流解析规则

对于 text 和 stream source：

- 忽略空白行
- 忽略不包含 `=` 的 malformed line
- 只有第一个 `=` 用于分隔 key 和 value
- key 和 value 都会被 trim

例如，`Key = a=b=c` 会得到 key `Key` 和 value `a=b=c`。

## 重新加载与变更信号

- `ReloadAsync()` 只有在发布了新的 snapshot 实例时才返回 `true`
- `ReloadAsync()` 在保留当前 snapshot 实例时返回 `false`
- `GetChangeSignal()` 返回当前已发布版本对应的 one-shot signal
- 每次 `ReloadAsync()` 调用最多只会发布一个新的组合 snapshot
- 在一次已发布的变化之后，如果要等待后续变化，请重新获取一个新的 signal，因为 signal 与单个已发布版本绑定

如果 reload 在某些 provider 已经发布了新的 snapshot 版本之后发生异常或被取消，
root 可能会在 reload task 全部 settle 之后，先发布这些已 settle provider version 所观察到的组合 snapshot，
然后再重新抛出该失败。发生失败的 reload 之后，如果你需要观察最新已发布状态，应重新读取 `Snapshot` 并获取新的 change signal。

当内置 source 使用 `versionStampFactory` 时，第一次成功完成的 materialization 会建立一个已接受的 authoritative stamp baseline。
之后任何成功完成的 rematerialization，即使因为 materialized content 未变化而保留当前 snapshot 实例，也会推进这个 baseline。
之后相等的 stamp（包括重复的 `null`）会跳过 reread、reparse 或 re-enumeration 工作。
stamp 发生变化时会强制 rematerialization，但如果 materialized content 未变化，当前 snapshot 仍可能被保留。

当所有组合后的 provider snapshot 都是 PicoCfg 的原生 snapshot 类型时，root 会将它们扁平化为一个基于单个 dictionary 的 snapshot，以优化 steady-state 读取。
如果任一 provider 提供的是自定义 `ICfgSnapshot`，root 会保留该自定义 lookup 行为，并回退为 read-time provider scanning，而不是把这些自定义语义扁平化掉。
即便在 fallback 组合模式下，仍然遵守正常优先级：后面的 provider 会覆盖前面的 provider。

## 自定义来源

自定义集成基于 `PicoCfg.Abs`。

- `ICfgSource.OpenAsync()` 会把一个 source 打开成一个长期存在的 provider
- 返回的 provider 必须已经暴露出一个可读的 `Snapshot`
- `ICfgProvider.ReloadAsync()` 用于报告该 provider 是否发布了新的 snapshot 实例；当返回 `false` 时，表示该 provider version authoritative unchanged，调用方可以继续持有当前 snapshot 引用
- `ICfgProvider.GetChangeSignal()` 返回当前已发布版本对应的 one-shot signal

最小示例：

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

PicoCfg 被设计为保持对 Native AOT 场景友好。
仓库中包含 `samples/PicoCfg.Sample`，并在 CI 中使用 `dotnet publish -p:PublishAOT=true` 进行验证。

示例：

```bash
dotnet publish samples/PicoCfg.Sample \
  -c Release \
  -r win-x64 \
  -p:PublishAOT=true \
  --self-contained
```

请根据目标平台调整 runtime identifier。

## 构建与测试

这些命令与仓库 CI workflow 保持一致：

```bash
dotnet restore tests/PicoCfg.Tests/PicoCfg.Tests.csproj -p:UseProjectReferences=true
dotnet build tests/PicoCfg.Tests/PicoCfg.Tests.csproj --configuration Release --no-restore -p:UseProjectReferences=true
dotnet test --project tests/PicoCfg.Tests/PicoCfg.Tests.csproj --configuration Release --no-build --verbosity normal -p:UseProjectReferences=true
```

本地运行 sample：

```bash
dotnet run --project samples/PicoCfg.Sample/PicoCfg.Sample.csproj
dotnet run --project samples/PicoCfg.Gen.Sample/PicoCfg.Gen.Sample.csproj
```

## 许可证

MIT License.
