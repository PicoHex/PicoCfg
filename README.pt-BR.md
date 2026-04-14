# PicoCfg

[English](README.md) | [简体中文](README.zh.md) | [繁體中文](README.zh-TW.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Français](README.fr.md) | [日本語](README.ja.md) | [Português (Brasil)](README.pt-BR.md) | [Русский](README.ru.md)

[![CI](https://github.com/PicoHex/PicoCfg/actions/workflows/ci.yml/badge.svg)](https://github.com/PicoHex/PicoCfg/actions/workflows/ci.yml)

PicoCfg é uma biblioteca de configuração pequena e compatível com AOT para .NET.
Ela compõe múltiplas fontes em um snapshot estável e somente leitura, oferece suporte a reload explícito e expõe um one-shot change signal para atualizações publicadas.

## Por que PicoCfg

- superfície pública pequena
- busca exata por chave string
- semântica explícita de reload e change signal
- suporte a fontes personalizadas via `PicoCfg.Abs`
- design compatível com Native AOT

## Instalação

A maioria das aplicações precisa apenas de `PicoCfg`:

```bash
dotnet add package PicoCfg
```

Use `PicoCfg.Abs` quando você precisar apenas dos contratos para integrações ou abstrações personalizadas:

```bash
dotnet add package PicoCfg.Abs
```

## Início rápido

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

Fontes adicionadas depois sobrescrevem fontes adicionadas antes.

## Semântica principal

### Busca exata por chave

`GetValue()` realiza uma busca exata de string completa sobre o snapshot atual e retorna `null` quando a chave está ausente.
Caracteres como `:` e `.` fazem parte do nome da chave; o PicoCfg não os interpreta como travessia hierárquica.

### Snapshots estáveis

`ICfgRoot.Snapshot` expõe o snapshot somente leitura atualmente publicado.
Se um reload não publicar um novo snapshot, a mesma instância de snapshot é mantida.
A publicação do root segue a sequência composta de provider snapshots, e não apenas os valores visíveis finais mesclados.
Snapshots já obtidos continuam utilizáveis após o dispose do root.

### Ciclo de vida

O root construído é proprietário dos providers abertos e implementa `IAsyncDisposable`.
Prefira `await using` para o uso normal.
O dispose libera os providers possuídos, mas não invalida snapshots nem change signals específicas de versão que já tenham sido obtidas.

## Tipos de fonte

### Fonte de string

```csharp
builder.Add("Key1=Value1\nKey2=Value2");
```

É analisada como conteúdo de texto baseado em linhas no formato `key=value`.

### Fonte de dicionário

```csharp
builder.Add(new Dictionary<string, string>
{
    ["RawValue"] = "a=b=c",
    ["MultiLine"] = "line1\nline2",
});
```

Os valores do dicionário são usados como estão.
Eles não são reanalisados como texto, portanto caracteres `=` embutidos e conteúdo multilinha são preservados.

### Fonte de fluxo

```csharp
builder.Add(() => File.OpenRead("app.cfg"));
```

É reanalisada em cada reload usando o mesmo formato baseado em linhas `key=value` do conteúdo string.

### Regras de análise de texto e fluxo

Para fontes de texto e stream:

- linhas em branco são ignoradas
- linhas malformadas sem `=` são ignoradas
- apenas o primeiro `=` divide a chave e o valor
- chaves e valores são trimados

Por exemplo, `Key = a=b=c` produz a chave `Key` e o valor `a=b=c`.

## Recarregamento e sinais de mudança

- `ReloadAsync()` retorna `true` somente quando uma nova instância de snapshot é publicada
- `ReloadAsync()` retorna `false` quando a instância atual do snapshot é mantida
- `GetChangeSignal()` retorna o one-shot signal da versão atualmente publicada
- cada chamada de `ReloadAsync()` publica no máximo um novo snapshot composto
- após uma mudança publicada, obtenha um novo signal para esperas futuras porque signals estão vinculados a uma única versão publicada

Se um reload lançar exceção ou for cancelado depois que alguns providers já tiverem publicado novas versões de snapshot,
o root pode primeiro publicar o snapshot composto observado para essas versões de provider já estabilizadas, depois que as tarefas de reload tiverem settle,
e então relançar a falha. Após um reload com falha, releia `Snapshot` e obtenha um novo change signal se precisar observar o estado publicado mais recente.

Quando uma fonte embutida usa `versionStampFactory`, a primeira materialização concluída estabelece uma baseline de stamp autoritativa aceita.
Qualquer rematerialização concluída depois disso atualiza essa baseline, mesmo quando a instância atual do snapshot é mantida porque o conteúdo materializado não mudou.
Stamps iguais posteriores, incluindo `null` repetido, pulam trabalho de reread, reparse ou re-enumeration.
Um stamp alterado força rematerialização, mas o snapshot atual ainda pode ser mantido quando o conteúdo materializado não muda.

Quando todos os provider snapshots compostos são do tipo snapshot nativo do PicoCfg, o root os achata em um único snapshot baseado em dicionário para leituras em estado estável.
Se qualquer provider fornecer um `ICfgSnapshot` personalizado, o root preserva esse comportamento personalizado de lookup e recorre a varredura de providers em tempo de leitura,
em vez de achatar essa semântica personalizada. A composição de fallback ainda respeita a precedência normal: providers posteriores sobrescrevem providers anteriores.

## Fontes personalizadas

Integrações personalizadas são construídas sobre `PicoCfg.Abs`.

- `ICfgSource.OpenAsync()` abre uma fonte em um provider de longa duração
- o provider retornado já deve expor um `Snapshot` legível
- `ICfgProvider.ReloadAsync()` informa se esse provider publicou uma nova instância de snapshot; `false` significa authoritative unchanged para aquela versão do provider, e os chamadores podem manter a referência atual do snapshot
- `ICfgProvider.GetChangeSignal()` retorna o one-shot signal da versão atualmente publicada

Esboço mínimo:

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

PicoCfg foi projetado para continuar amigável a cenários Native AOT.
O repositório inclui `samples/PicoCfg.Sample` e validação em CI usando `dotnet publish -p:PublishAOT=true`.

Exemplo:

```bash
dotnet publish samples/PicoCfg.Sample \
  -c Release \
  -r win-x64 \
  -p:PublishAOT=true \
  --self-contained
```

Ajuste o runtime identifier para sua plataforma de destino.

## Compilação e testes

Estes comandos correspondem ao workflow de CI do repositório:

```bash
dotnet restore tests/PicoCfg.Tests/PicoCfg.Tests.csproj -p:UseProjectReferences=true
dotnet build tests/PicoCfg.Tests/PicoCfg.Tests.csproj --configuration Release --no-restore -p:UseProjectReferences=true
dotnet test --project tests/PicoCfg.Tests/PicoCfg.Tests.csproj --configuration Release --no-build --verbosity normal -p:UseProjectReferences=true
```

Execute o sample localmente:

```bash
dotnet run --project samples/PicoCfg.Sample/PicoCfg.Sample.csproj
```

## Licença

MIT License.
