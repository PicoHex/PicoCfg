# PicoCfg

[English](README.md) | [简体中文](README.zh.md) | [繁體中文](README.zh-TW.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Français](README.fr.md) | [日本語](README.ja.md) | [Português (Brasil)](README.pt-BR.md) | [Русский](README.ru.md)

[![CI](https://github.com/PicoHex/PicoCfg/actions/workflows/ci.yml/badge.svg)](https://github.com/PicoHex/PicoCfg/actions/workflows/ci.yml)

PicoCfg 是一个面向 .NET 的小型、AOT 友好的配置库。
它可以将多个来源组合成稳定的只读快照，支持显式 reload，并为已发布的更新提供 one-shot change signal。

## 为什么选择 PicoCfg

- 小型公共接口面
- 精确字符串键查找
- 显式 reload 和 change-signal 语义
- 通过 `PicoCfg.Abs` 提供最小化消费契约
- 面向 Native AOT 的设计

## 安装

大多数应用只需要 `PicoCfg`：

```bash
dotnet add package PicoCfg
```

如果你只需要最小化消费契约，例如 `ICfg` 和 `ICfgRoot`，请使用 `PicoCfg.Abs`：

```bash
dotnet add package PicoCfg.Abs
```

如果你想把 PicoCfg 的配置视图以 AOT 友好的方式通过源码生成绑定到平坦 POCO，请使用 `PicoCfg.Gen`。该包提供生成器，而 `CfgBind` 运行时 API 位于 `PicoCfg` 中：

```bash
dotnet add package PicoCfg.Gen
```

如果你想使用 `PicoCfg.DI` 为 `ICfgRoot`、`ICfg` 以及基于生成绑定的配置服务提供 PicoDI 注册辅助，请使用 `PicoCfg.DI`。如果你在消费应用中需要 `SvcContainer` 这样的运行时容器实现，请额外安装 `PicoDI`。在 project-reference 模式下，请在使用方应用中保留对 `PicoCfg.Gen` 的直接引用，这样 binder generator 才会为你的 `RegisterCfg*<T>` 调用运行：

```bash
dotnet add package PicoCfg.DI
dotnet add package PicoCfg.Gen
dotnet add package PicoDI
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

Console.WriteLine(root.GetValue("ConnectionString"));
Console.WriteLine(root.GetValue("Logging:Level"));
```

后添加的 source 会覆盖先添加的 source。

## 使用 PicoCfg.Gen 进行生成绑定

`PicoCfg.Gen` 为 PicoCfg 的精确键模型提供源码生成器，而 `PicoCfg` 提供生成绑定器使用的 `CfgBind` 运行时 API。
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

在引用 `PicoCfg.Gen` 后，可用的生成绑定 API 为：

- `CfgBind.Bind<T>(ICfgRoot, section?)`
- `CfgBind.Bind<T>(ICfg, section?)`
- `CfgBind.TryBind<T>(...)`
- `CfgBind.BindInto<T>(...)`

### PicoCfg.Gen v1 范围

当前生成绑定器刻意保持较小范围：

- 仅支持 direct closed generic `CfgBind` 调用
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

## PicoCfg.DI 与 PicoDI

`PicoCfg.DI` 在 `PicoCfg` 和 `PicoCfg.Gen` 之上增加了对 PicoDI 友好的注册辅助。
当你已经拥有 `ICfgRoot` 时使用 `RegisterCfgRoot(...)`，当你想通过 PicoDI 解析基于生成绑定的 POCO 时使用 `RegisterCfgTransient<T>()` / `RegisterCfgScoped<T>()` / `RegisterCfgSingleton<T>()`。

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

仓库还包含 `samples/PicoCfg.DI.Sample`，用于展示一个完整的 PicoDI 集成示例。

## 核心语义

### 精确键查找

`GetValue()` 会对当前 snapshot 执行精确的完整字符串查找；如果 key 不存在，则返回 `null`。
像 `:` 和 `.` 这样的字符是 key 名称的一部分；PicoCfg 不会把它们解释成层级遍历。

### 已发布的配置视图 [advanced]

`ICfgRoot` 始终从当前已发布的组合配置视图中读取。
如果 reload 没有发布新的视图，读取仍会继续观察相同的已发布状态。
Root 的发布依据是组合后的 provider snapshot 序列，而不只是最终合并后的可见值。

大多数应用代码应继续使用 `ICfg` 进行精确查找，使用 `ICfgRoot` 处理所有权、reload 与等待语义，使用已绑定的 POCO 进行类型化消费。

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
- 每次 `ReloadAsync()` 调用最多只会发布一个新的组合 snapshot
- `WaitForChangeAsync()` 是面向 root 使用方的公共变更通知原语

如果 reload 在某些 provider 已经发布了新的 snapshot 版本之后发生异常或被取消，
root 可能会在 reload task 全部 settle 之后，先发布这些已 settle provider version 所观察到的组合 snapshot，
然后再重新抛出该失败。发生失败的 reload 之后，如果你需要观察最新已发布状态，应通过 `ICfgRoot` 或 `ICfg` 重新读取。

当内置 source 使用 `versionStampFactory` 时，第一次成功完成的 materialization 会建立一个已接受的 authoritative stamp baseline。
之后任何成功完成的 rematerialization，即使因为 materialized content 未变化而保留当前 snapshot 实例，也会推进这个 baseline。
之后相等的 stamp（包括重复的 `null`）会跳过 reread、reparse 或 re-enumeration 工作。
stamp 发生变化时会强制 rematerialization，但如果 materialized content 未变化，当前 snapshot 仍可能被保留。

内置组合会保留正常优先级，后添加的 source 会覆盖先添加的 source。PicoCfg 可能在内部优化稳定状态下的读取，但不会改变通过 `ICfg` 和 `ICfgRoot` 暴露的精确键行为。

## 自定义来源

`PicoCfg.Abs` 现在专注于最小化的面向消费方契约。更底层的 source 与 provider 组合钩子属于实现细节，不属于主要的应用侧 API。

## 高级自定义

主要公共 API 刻意保持精简，只包含 `ICfg`、`ICfgRoot`、`CfgBind` 以及基于它们构建的 DI 辅助。更底层的 builder 与组合钩子保持内部实现状态。

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
dotnet test --project tests/PicoCfg.DI.Tests/PicoCfg.DI.Tests.csproj --configuration Release --no-build --verbosity normal -p:UseProjectReferences=true
```

本地运行 sample：

```bash
dotnet run --project samples/PicoCfg.Sample/PicoCfg.Sample.csproj
dotnet run --project samples/PicoCfg.Gen.Sample/PicoCfg.Gen.Sample.csproj
dotnet run --project samples/PicoCfg.DI.Sample/PicoCfg.DI.Sample.csproj
```

## 许可证

MIT License.
