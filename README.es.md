# PicoCfg

[English](README.md) | [简体中文](README.zh.md) | [繁體中文](README.zh-TW.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Français](README.fr.md) | [日本語](README.ja.md) | [Português (Brasil)](README.pt-BR.md) | [Русский](README.ru.md)

[![CI](https://github.com/PicoHex/PicoCfg/actions/workflows/ci.yml/badge.svg)](https://github.com/PicoHex/PicoCfg/actions/workflows/ci.yml)

PicoCfg es una biblioteca de configuración pequeña y compatible con AOT para .NET.
Compone múltiples fuentes en un snapshot estable de solo lectura, admite reload explícito y expone una one-shot change signal para actualizaciones publicadas.

## Por qué PicoCfg

- superficie pública pequeña
- búsqueda exacta por clave de tipo string
- semántica explícita de reload y change signal
- soporte para fuentes personalizadas mediante `PicoCfg.Abs`
- diseño compatible con Native AOT

## Instalación

La mayoría de las aplicaciones solo necesitan `PicoCfg`:

```bash
dotnet add package PicoCfg
```

Usa `PicoCfg.Abs` cuando solo necesites los contratos para integraciones o abstracciones personalizadas:

```bash
dotnet add package PicoCfg.Abs
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

Console.WriteLine(root.Snapshot.GetValue("ConnectionString"));
Console.WriteLine(root.Snapshot.GetValue("Logging:Level"));
```

Las fuentes agregadas más tarde reemplazan a las agregadas antes.

## Semántica principal

### Búsqueda exacta por clave

`GetValue()` realiza una búsqueda exacta de cadena completa sobre el snapshot actual y devuelve `null` cuando la clave no existe.
Caracteres como `:` y `.` forman parte del nombre de la clave; PicoCfg no los interpreta como recorrido jerárquico.

### Snapshots estables

`ICfgRoot.Snapshot` expone el snapshot publicado actualmente en modo de solo lectura.
Si un reload no publica un nuevo snapshot, se conserva la misma instancia de snapshot.
La publicación del root sigue la secuencia compuesta de provider snapshots, no solo los valores visibles finales ya combinados.
Los snapshots ya obtenidos siguen siendo utilizables después del dispose del root.

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
- `GetChangeSignal()` devuelve la one-shot signal para la versión publicada actual
- cada llamada a `ReloadAsync()` publica como máximo un nuevo snapshot compuesto
- después de un cambio publicado, obtén una nueva signal para esperas posteriores porque las signals están ligadas a una única versión publicada

Si un reload lanza una excepción o es cancelado después de que algunos providers ya hayan publicado nuevas versiones de snapshot,
el root puede primero publicar el snapshot compuesto observado para esas versiones de provider ya asentadas, después de que las tareas de reload se estabilicen,
y luego volver a lanzar el fallo. Después de un reload fallido, vuelve a leer `Snapshot` y obtén una nueva change signal si necesitas observar el último estado publicado.

Cuando una fuente integrada usa `versionStampFactory`, la primera materialización completada establece una baseline de stamp autoritativa aceptada.
Cualquier rematerialización completada posteriormente actualiza esa baseline incluso cuando la instancia actual del snapshot se conserva porque el contenido materializado no cambió.
Los stamps iguales posteriores, incluidos `null` repetidos, omiten el trabajo de reread, reparse o re-enumeration.
Un stamp cambiado fuerza una rematerialización, pero el snapshot actual aún puede conservarse cuando el contenido materializado no cambia.

Cuando todos los provider snapshots compuestos son snapshots nativos de PicoCfg, el root los aplana en un único snapshot respaldado por diccionario para lecturas de estado estable.
Si algún provider suministra un `ICfgSnapshot` personalizado, el root preserva ese comportamiento personalizado de lookup y recurre a escaneo de providers en tiempo de lectura,
en lugar de aplanar esa semántica personalizada. La composición de fallback sigue respetando la precedencia normal: los providers posteriores sobrescriben a los anteriores.

## Fuentes personalizadas

Las integraciones personalizadas se construyen sobre `PicoCfg.Abs`.

- `ICfgSource.OpenAsync()` abre una fuente en un provider de larga duración
- el provider devuelto ya debe exponer un `Snapshot` legible
- `ICfgProvider.ReloadAsync()` informa si ese provider publicó una nueva instancia de snapshot; `false` significa authoritative unchanged para esa versión del provider, y los llamadores pueden conservar la referencia actual al snapshot
- `ICfgProvider.GetChangeSignal()` devuelve la one-shot signal para la versión publicada actual

Esquema mínimo:

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
```

Ejecuta el sample localmente:

```bash
dotnet run --project samples/PicoCfg.Sample/PicoCfg.Sample.csproj
```

## Licencia

MIT License.
