# PicoCfg

[![CI](https://github.com/PicoHex/PicoCfg/actions/workflows/ci.yml/badge.svg)](https://github.com/PicoHex/PicoCfg/actions/workflows/ci.yml)

A lightweight, async-first configuration management framework from PicoHex, designed for AOT compatibility and edge computing scenarios.

## Features

- **Async-first API**: All operations support async/await with cancellation tokens
- **AOT-compatible**: No reflection, dynamic code generation, or runtime type discovery
- **Multiple configuration sources**: Support for strings, dictionaries, streams, and custom sources
- **Change notification**: Built-in support for configuration change monitoring
- **Priority-based override**: Later sources override earlier ones
- **Minimal dependencies**: Self-contained implementation with no external dependencies
- **SourceLink support**: Source-level debugging from NuGet packages

## Installation

```bash
dotnet add package PicoCfg.Abs
dotnet add package PicoCfg
```

## Quick Start

```csharp
using PicoCfg;
using PicoCfg.Extensions;

var builder = CFG.CreateBuilder();

builder
    .Add("Database.ConnectionString=localhost:3306")
    .Add(new Dictionary<string, string> { ["Logging:Level"] = "Debug" })
    .Add(() => new MemoryStream(Encoding.UTF8.GetBytes("AppName=MyTestApp")));

var configRoot = await builder.BuildAsync();

var connectionString = await configRoot.GetValueAsync("Database.ConnectionString");
Console.WriteLine($"Connection: {connectionString}");
```

## Architecture

PicoCfg follows a clean abstraction/implementation separation:

- **PicoCfg.Abs**: Interface contracts for configuration management
- **PicoCfg**: Concrete implementations and extension methods
- **Extensions**: Convenient helper methods for common scenarios

## AOT Compatibility

PicoCfg is fully compatible with Native AOT compilation:

- No reflection API usage
- No dynamic code generation
- Pure interface-driven design
- Compile-time type resolution

To publish with AOT:

```bash
dotnet publish -c Release -r win-x64 -p:PublishAOT=true --self-contained
```

## Building from Source

```bash
git clone https://github.com/PicoHex/PicoCfg.git
cd PicoCfg
dotnet build --configuration Release
```

## Contributing

This project follows the PicoHex organization's development standards:
- AOT-compatible code patterns
- Deterministic builds
- SourceLink integration
- Comprehensive CI/CD pipeline

## License

MIT License - see LICENSE file for details.
