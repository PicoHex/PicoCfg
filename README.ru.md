# PicoCfg

[English](README.md) | [简体中文](README.zh.md) | [繁體中文](README.zh-TW.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Français](README.fr.md) | [日本語](README.ja.md) | [Português (Brasil)](README.pt-BR.md) | [Русский](README.ru.md)

[![CI](https://github.com/PicoHex/PicoCfg/actions/workflows/ci.yml/badge.svg)](https://github.com/PicoHex/PicoCfg/actions/workflows/ci.yml)

PicoCfg — это небольшая AOT-friendly библиотека конфигурации для .NET.
Она объединяет несколько источников в стабильный snapshot только для чтения, поддерживает явный reload и предоставляет one-shot change signal для опубликованных обновлений.

## Почему PicoCfg

- небольшая публичная поверхность
- точный поиск по строковому ключу
- явная семантика reload и change signal
- минимальные consumer-контракты через `PicoCfg.Abs`
- дизайн, дружественный к Native AOT

## Установка

Большинству приложений нужен только `PicoCfg`:

```bash
dotnet add package PicoCfg
```

Используйте `PicoCfg.Abs`, если вам нужны только минимальные consumer-контракты, такие как `ICfg` и `ICfgRoot`:

```bash
dotnet add package PicoCfg.Abs
```

Используйте `PicoCfg.Gen`, если хотите AOT-safe образом связывать конфигурационные представления PicoCfg с плоскими POCO через source generation. Пакет добавляет генератор, а runtime API `CfgBind` находится в `PicoCfg`:

```bash
dotnet add package PicoCfg.Gen
```

Используйте `PicoCfg.DI`, если вам нужны совместимые с PicoDI средства регистрации для `ICfgRoot`, `ICfg` и сервисов конфигурации на основе generated binding. Если приложению-потребителю нужна runtime-реализация контейнера, такая как `SvcContainer`, дополнительно подключите `PicoDI`. В режиме project-reference сохраняйте прямую ссылку на `PicoCfg.Gen` в приложении-потребителе, чтобы binder generator запускался для ваших вызовов `RegisterCfg*<T>`:

```bash
dotnet add package PicoCfg.DI
dotnet add package PicoCfg.Gen
dotnet add package PicoDI
```

## Быстрый старт

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

Более поздние source переопределяют более ранние.

## Генерируемое связывание с PicoCfg.Gen

`PicoCfg.Gen` добавляет source generator для модели точных ключей PicoCfg, а `PicoCfg` предоставляет runtime API `CfgBind`, которое использует сгенерированный binder.
Сгенерированный binder синхронный, trim-friendly и рассчитан на сценарии Native AOT.

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

При подключённом `PicoCfg.Gen` доступна следующая поверхность generated binding:

- `CfgBind.Bind<T>(ICfgRoot, section?)`
- `CfgBind.Bind<T>(ICfg, section?)`
- `CfgBind.TryBind<T>(...)`
- `CfgBind.BindInto<T>(...)`

### Границы PicoCfg.Gen v1

Текущий сгенерированный binder намеренно остаётся узким по возможностям:

- только прямые closed generic вызовы `CfgBind`
- только concrete class target
- только flat public writable scalar properties
- точное совпадение имени свойства с учётом регистра
- необязательная композиция префикса `section:` поверх точных ключей PicoCfg

Неподдерживаемые формы приводят к build diagnostics вместо runtime reflection fallback, включая:

- вложенные или сложные object properties
- collection properties
- open generic targets
- неподдерживаемые типы свойств

`Bind<T>` и `TryBind<T>` требуют public parameterless constructor.
`BindInto<T>` всё ещё может записывать в существующий экземпляр без него.

В репозитории также есть `samples/PicoCfg.Gen.Sample` как небольшой end-to-end пример генерируемого связывания.

## PicoCfg.DI с PicoDI

`PicoCfg.DI` добавляет к `PicoCfg` и `PicoCfg.Gen` совместимые с PicoDI средства регистрации.
Используйте `RegisterCfgRoot(...)`, если у вас уже есть `ICfgRoot`, и `RegisterCfgTransient<T>()` / `RegisterCfgScoped<T>()` / `RegisterCfgSingleton<T>()`, если вы хотите разрешать через PicoDI POCO на основе generated binding.

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

В репозитории также есть `samples/PicoCfg.DI.Sample` как небольшой end-to-end пример интеграции с PicoDI.

## Основная семантика

### Точный поиск по ключу

`GetValue()` выполняет точный поиск по всей строке в текущем snapshot и возвращает `null`, если ключ отсутствует.
Символы вроде `:` и `.` являются частью имени ключа; PicoCfg не трактует их как иерархическую навигацию.

### Опубликованные конфигурационные представления [advanced]

`ICfgRoot` всегда читает из текущего опубликованного составного конфигурационного представления.
Если reload не публикует новое представление, чтения продолжают видеть то же опубликованное состояние.
Публикация root определяется составной последовательностью provider snapshot, а не только финальными объединёнными видимыми значениями.

Большинству прикладного кода стоит использовать `ICfg` для точных lookup, `ICfgRoot` для семантики владения, reload и ожидания, а связанные POCO для типизированного потребления.

### Жизненный цикл

Построенный root владеет открытыми provider и реализует `IAsyncDisposable`.
Для обычного использования рекомендуется `await using`.
Dispose освобождает принадлежащие provider, но не инвалидирует уже полученные snapshot или change signal, привязанные к конкретной версии.

## Типы источников

### Строковый источник

```csharp
builder.Add("Key1=Value1\nKey2=Value2");
```

Разбирается как построчный текст в формате `key=value`.

### Источник словаря

```csharp
builder.Add(new Dictionary<string, string>
{
    ["RawValue"] = "a=b=c",
    ["MultiLine"] = "line1\nline2",
});
```

Значения словаря используются как есть.
Они не разбираются повторно как текст, поэтому встроенные символы `=` и многострочное содержимое сохраняются.

### Потоковый источник

```csharp
builder.Add(() => File.OpenRead("app.cfg"));
```

Повторно разбирается при каждом reload с использованием того же построчного формата `key=value`, что и строковое содержимое.

### Правила разбора текста и потока

Для text и stream source:

- пустые строки игнорируются
- некорректные строки без `=` игнорируются
- только первый `=` разделяет ключ и значение
- ключи и значения обрезаются (`trim`)

Например, `Key = a=b=c` даёт ключ `Key` и значение `a=b=c`.

## Перезагрузка и сигналы изменений

- `ReloadAsync()` возвращает `true` только тогда, когда публикуется новый экземпляр snapshot
- `ReloadAsync()` возвращает `false`, когда текущий экземпляр snapshot сохраняется
- каждый вызов `ReloadAsync()` публикует не более одного нового составного snapshot
- `WaitForChangeAsync()` является публичным primitive уведомления об изменениях для consumer'ов root

Если reload выбрасывает исключение или отменяется после того, как некоторые provider уже опубликовали новые версии snapshot,
root может сначала опубликовать наблюдаемый составной snapshot для этих уже settled provider version после того, как reload task завершат settle,
а затем повторно выбросить ошибку. После неудачного reload перечитайте через `ICfgRoot` или `ICfg`, если вам нужно наблюдать последнее опубликованное состояние.

Когда встроенный source использует `versionStampFactory`, первая завершённая materialization устанавливает accepted authoritative stamp baseline.
Любая последующая завершённая rematerialization обновляет эту baseline, даже если текущий экземпляр snapshot сохраняется, потому что materialized content не изменился.
Последующие одинаковые stamp, включая повторяющийся `null`, пропускают reread, reparse или re-enumeration работу.
Изменившийся stamp принудительно запускает rematerialization, но текущий snapshot всё ещё может быть сохранён, если materialized content не изменился.

Встроенная композиция сохраняет обычный приоритет: более поздние source перекрывают более ранние. PicoCfg может внутренне оптимизировать steady-state чтения, не меняя поведение точного ключа, которое видно через `ICfg` и `ICfgRoot`.

## Пользовательские источники

`PicoCfg.Abs` теперь сосредоточен на минимальных контрактах для потребителей. Более низкоуровневые hooks для композиции source и provider являются деталями реализации, а не частью основной API для приложений.

## Расширенная настройка

Основная публичная API намеренно остаётся небольшой: `ICfg`, `ICfgRoot`, `CfgBind` и DI-хелперы поверх них. Более низкоуровневые hooks builder и composition остаются внутренними.

## Native AOT

PicoCfg спроектирован так, чтобы оставаться дружественным к сценариям Native AOT.
Репозиторий включает `samples/PicoCfg.Sample` и проверку в CI с использованием `dotnet publish -p:PublishAOT=true`.

Пример:

```bash
dotnet publish samples/PicoCfg.Sample \
  -c Release \
  -r win-x64 \
  -p:PublishAOT=true \
  --self-contained
```

Подберите runtime identifier под целевую платформу.

## Сборка и тестирование

Эти команды соответствуют CI workflow репозитория:

```bash
dotnet restore tests/PicoCfg.Tests/PicoCfg.Tests.csproj -p:UseProjectReferences=true
dotnet build tests/PicoCfg.Tests/PicoCfg.Tests.csproj --configuration Release --no-restore -p:UseProjectReferences=true
dotnet test --project tests/PicoCfg.Tests/PicoCfg.Tests.csproj --configuration Release --no-build --verbosity normal -p:UseProjectReferences=true
dotnet test --project tests/PicoCfg.Gen.Tests/PicoCfg.Gen.Tests.csproj --configuration Release --no-build --verbosity normal -p:UseProjectReferences=true
dotnet test --project tests/PicoCfg.DI.Tests/PicoCfg.DI.Tests.csproj --configuration Release --no-build --verbosity normal -p:UseProjectReferences=true
```

Запуск sample локально:

```bash
dotnet run --project samples/PicoCfg.Sample/PicoCfg.Sample.csproj
dotnet run --project samples/PicoCfg.Gen.Sample/PicoCfg.Gen.Sample.csproj
dotnet run --project samples/PicoCfg.DI.Sample/PicoCfg.DI.Sample.csproj
```

## Лицензия

MIT License.
