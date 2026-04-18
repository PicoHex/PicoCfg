# PicoCfg

[English](README.md) | [简体中文](README.zh.md) | [繁體中文](README.zh-TW.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Français](README.fr.md) | [日本語](README.ja.md) | [Português (Brasil)](README.pt-BR.md) | [Русский](README.ru.md)

[![CI](https://github.com/PicoHex/PicoCfg/actions/workflows/ci.yml/badge.svg)](https://github.com/PicoHex/PicoCfg/actions/workflows/ci.yml)

PicoCfg es una biblioteca de configuración pequeña y compatible con AOT para .NET.
Compone múltiples fuentes en un snapshot estable de solo lectura, admite reload explícito y expone una one-shot change signal para actualizaciones publicadas.

## Por qué PicoCfg

- superficie pública pequeña
- búsqueda exacta por clave de tipo string
- semántica explícita de reload y change signal
- contratos de consumo mínimos mediante `PicoCfg.Abs`
- diseño compatible con Native AOT

## Instalación

La mayoría de las aplicaciones solo necesitan `PicoCfg`:

```bash
dotnet add package PicoCfg
```

Usa `PicoCfg.Abs` cuando solo necesites los contratos mínimos de consumo, como `ICfg` e `ICfgRoot`:

```bash
dotnet add package PicoCfg.Abs
```

Usa `PicoCfg.Gen` cuando quieras enlazar vistas de configuración de PicoCfg a POCOs planos de forma segura para AOT mediante source generation. El paquete aporta el generador, mientras que la API runtime `CfgBind` vive en `PicoCfg`:

```bash
dotnet add package PicoCfg.Gen
```

Usa `PicoCfg.DI` cuando quieras helpers de registro compatibles con PicoDI para `ICfgRoot`, `ICfg` y servicios de configuración respaldados por binding generado. En modo project-reference, mantén una referencia directa a `PicoCfg.Gen` en la aplicación que consume el paquete para que el generador del binder se ejecute para tus llamadas `RegisterCfg*<T>`:

```bash
dotnet add package PicoCfg.DI
dotnet add package PicoCfg.Gen
```

## Inicio rápido

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

Las fuentes agregadas más tarde reemplazan a las agregadas antes.

## Enlace generado con PicoCfg.Gen

`PicoCfg.Gen` aporta el generador de código para el modelo de claves exactas de PicoCfg, mientras que `PicoCfg` proporciona la API runtime `CfgBind` que usa el binder generado.
El binder generado es síncrono, compatible con trim y está pensado para escenarios Native AOT.

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

Con `PicoCfg.Gen` referenciado, la superficie de enlace generado disponible es:

- `CfgBind.Bind<T>(ICfgRoot, section?)`
- `CfgBind.Bind<T>(ICfg, section?)`
- `CfgBind.TryBind<T>(...)`
- `CfgBind.BindInto<T>(...)`

### Alcance de PicoCfg.Gen v1

El binder generado actual mantiene intencionadamente un alcance reducido:

- solo llamadas directas cerradas genéricas a `CfgBind`
- solo destinos de clase concreta
- solo propiedades escalares públicas y escribibles en estructuras planas
- coincidencia exacta y sensible a mayúsculas/minúsculas del nombre de propiedad
- composición opcional del prefijo `section:` sobre las claves exactas de PicoCfg

Las formas no admitidas producen diagnósticos de compilación en lugar de un fallback por reflexión en tiempo de ejecución, incluyendo:

- propiedades de objetos anidadas o complejas
- propiedades de colección
- destinos open generic
- tipos de propiedad no admitidos

`Bind<T>` y `TryBind<T>` requieren un constructor público sin parámetros.
`BindInto<T>` puede seguir escribiendo sobre una instancia existente sin ese constructor.

El repositorio también incluye `samples/PicoCfg.Gen.Sample` como ejemplo pequeño de enlace generado end-to-end.

## PicoCfg.DI con PicoDI

`PicoCfg.DI` agrega helpers de registro compatibles con PicoDI sobre `PicoCfg` y `PicoCfg.Gen`.
Usa `RegisterCfgRoot(...)` cuando ya tengas un `ICfgRoot`, y `RegisterCfgTransient<T>()` / `RegisterCfgScoped<T>()` / `RegisterCfgSingleton<T>()` cuando quieras resolver mediante PicoDI POCOs respaldados por binding generado.

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

El repositorio también incluye `samples/PicoCfg.DI.Sample` como ejemplo pequeño de integración end-to-end con PicoDI.

## Semántica principal

### Búsqueda exacta por clave

`GetValue()` realiza una búsqueda exacta de cadena completa sobre el snapshot actual y devuelve `null` cuando la clave no existe.
Caracteres como `:` y `.` forman parte del nombre de la clave; PicoCfg no los interpreta como recorrido jerárquico.

### Vistas de configuración publicadas [advanced]

`ICfgRoot` siempre lee desde la vista de configuración compuesta publicada actualmente.
Si un reload no publica una vista nueva, las lecturas siguen observando el mismo estado publicado.
La publicación del root sigue la secuencia compuesta de provider snapshots, no solo los valores visibles finales ya combinados.

La mayor parte del código de aplicación debería quedarse con `ICfg` para búsquedas exactas, `ICfgRoot` para semántica de ownership, reload y espera, y POCOs enlazados para consumo tipado.

### Ciclo de vida

El root construido posee los providers abiertos e implementa `IAsyncDisposable`.
Se recomienda `await using` para el uso normal.
Dispose libera los providers que posee, pero no invalida snapshots ni change signals específicas de versión que ya se hayan obtenido.

## Tipos de fuente

### Fuente de cadena

```csharp
builder.Add("Key1=Value1\nKey2=Value2");
```

Se analiza como contenido de texto basado en líneas con formato `key=value`.

### Fuente de diccionario

```csharp
builder.Add(new Dictionary<string, string>
{
    ["RawValue"] = "a=b=c",
    ["MultiLine"] = "line1\nline2",
});
```

Los valores del diccionario se usan tal como están.
No se vuelven a analizar como texto, por lo que se conservan los caracteres `=` incrustados y el contenido multilínea.

### Fuente de flujo

```csharp
builder.Add(() => File.OpenRead("app.cfg"));
```

Se vuelve a analizar en cada reload usando el mismo formato basado en líneas `key=value` que el contenido string.

### Reglas de análisis de texto y flujo

Para fuentes de texto y stream:

- se ignoran las líneas en blanco
- se ignoran las líneas mal formadas sin `=`
- solo el primer `=` separa la clave del valor
- las claves y los valores se recortan

Por ejemplo, `Key = a=b=c` produce la clave `Key` y el valor `a=b=c`.

## Recarga y señales de cambio

- `ReloadAsync()` devuelve `true` solo cuando se publica una nueva instancia de snapshot
- `ReloadAsync()` devuelve `false` cuando se conserva la instancia actual del snapshot
- cada llamada a `ReloadAsync()` publica como máximo un nuevo snapshot compuesto
- `WaitForChangeAsync()` es la primitiva pública de notificación de cambios para los consumidores de root

Si un reload lanza una excepción o es cancelado después de que algunos providers ya hayan publicado nuevas versiones de snapshot,
el root puede primero publicar el snapshot compuesto observado para esas versiones de provider ya asentadas, después de que las tareas de reload se estabilicen,
y luego volver a lanzar el fallo. Después de un reload fallido, vuelve a leer mediante `ICfgRoot` o `ICfg` si necesitas observar el último estado publicado.

Cuando una fuente integrada usa `versionStampFactory`, la primera materialización completada establece una baseline de stamp autoritativa aceptada.
Cualquier rematerialización completada posteriormente actualiza esa baseline incluso cuando la instancia actual del snapshot se conserva porque el contenido materializado no cambió.
Los stamps iguales posteriores, incluidos `null` repetidos, omiten el trabajo de reread, reparse o re-enumeration.
Un stamp cambiado fuerza una rematerialización, pero el snapshot actual aún puede conservarse cuando el contenido materializado no cambia.

La composición integrada mantiene la precedencia normal: las fuentes agregadas después sobrescriben a las anteriores. PicoCfg puede optimizar internamente las lecturas en estado estable sin cambiar el comportamiento de clave exacta expuesto a través de `ICfg` e `ICfgRoot`.

## Fuentes personalizadas

`PicoCfg.Abs` ahora se centra en los contratos mínimos orientados al consumidor. Los hooks de composición de fuentes y providers de nivel inferior son detalles de implementación, no parte de la API principal orientada a aplicaciones.

## Personalización avanzada

La API pública principal se mantiene intencionadamente pequeña: `ICfg`, `ICfgRoot`, `CfgBind` y los helpers de DI construidos sobre ellos. Los hooks de builder y composición de nivel inferior permanecen internos.

## Native AOT

PicoCfg está diseñado para seguir siendo compatible con escenarios de Native AOT.
El repositorio incluye `samples/PicoCfg.Sample` y validación en CI usando `dotnet publish -p:PublishAOT=true`.

Ejemplo:

```bash
dotnet publish samples/PicoCfg.Sample \
  -c Release \
  -r win-x64 \
  -p:PublishAOT=true \
  --self-contained
```

Ajusta el runtime identifier para tu plataforma objetivo.

## Compilación y pruebas

Estos comandos coinciden con el workflow de CI del repositorio:

```bash
dotnet restore tests/PicoCfg.Tests/PicoCfg.Tests.csproj -p:UseProjectReferences=true
dotnet build tests/PicoCfg.Tests/PicoCfg.Tests.csproj --configuration Release --no-restore -p:UseProjectReferences=true
dotnet test --project tests/PicoCfg.Tests/PicoCfg.Tests.csproj --configuration Release --no-build --verbosity normal -p:UseProjectReferences=true
dotnet test --project tests/PicoCfg.DI.Tests/PicoCfg.DI.Tests.csproj --configuration Release --no-build --verbosity normal -p:UseProjectReferences=true
```

Ejecuta el sample localmente:

```bash
dotnet run --project samples/PicoCfg.Sample/PicoCfg.Sample.csproj
dotnet run --project samples/PicoCfg.Gen.Sample/PicoCfg.Gen.Sample.csproj
dotnet run --project samples/PicoCfg.DI.Sample/PicoCfg.DI.Sample.csproj
```

## Licencia

MIT License.
