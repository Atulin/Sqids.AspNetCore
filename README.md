# Sqids.AspNetCore

[![Coverage Status](https://coveralls.io/repos/github/Atulin/Sqids.AspNetCore/badge.svg?branch=master)](https://coveralls.io/github/Atulin/Sqids.AspNetCore?branch=master)
[![Tests & Coverage](https://github.com/Atulin/Sqids.AspNetCore/actions/workflows/coverage.yml/badge.svg)](https://github.com/Atulin/Sqids.AspNetCore/actions/workflows/coverage.yml)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Atulin.Sqids.AspNetCore)](https://www.nuget.org/packages/Atulin.Sqids.AspNetCore/)
[![NuGet Version](https://img.shields.io/nuget/v/Atulin.Sqids.AspNetCore)](https://www.nuget.org/packages/Atulin.Sqids.AspNetCore/)
[![GitHub License](https://img.shields.io/github/license/Atulin/Sqids.AspNetCore)](LICENSE)

A strongly-typed `Sqid` for ASP.NET Core, built on the [Sqids](https://sqids.org) ID generation scheme. It encodes non-negative integers into short, URL-safe, non-sequential IDs and decodes them back.

This library provides first-class integration with ASP.NET Core: route/query/form/model binding for Minimal APIs, MVC, and Razor Pages; JSON serialization/deserialization via a built-in `JsonConverter`; and OpenAPI schema generation. IDs are validated to be in canonical form, matching the behavior of other official Sqids implementations.

## Differences from [Sqids.NET](https://github.com/sqids/sqids-dotnet)

This library is not a fork of the core `Sqids` library; it builds a higher-level `Sqid` type and ASP.NET Core integration on top of the same spec. Key differences:

| Aspect | Sqids.AspNetCore | sqids-dotnet (core) |
|---|---|---|
| Primary focus | ASP.NET Core integration (binding, JSON, OpenAPI, route constraints) | Generic, framework-agnostic Sqids encoder/decoder |
| Type model | Strongly-typed `Sqid` `readonly struct` (value type) wrapping a single `long` value | `SqidsEncoder<T>` / `SqidsEncoder` for encoding/decoding numeric sequences |
| API surface | Focused on binding/parsing as a single ID value (`Parse`, `TryParse`, `From`, `Value`) | Focused on encoding/decoding numbers (`Encode`, `Decode`) with support for multiple numbers |
| Integration | Automatic model binding, `TypeConverter`, `{id:sqid}` route constraint, `JsonConverter`, OpenAPI schema (`AddSqidSchema`) | No ASP.NET Core-specific binding/JSON/OpenAPI helpers |
| Numeric types | Single integer (`long`) for the `Sqid` value | Generic math / overloads for many integral types (int, long, ulong, etc.) and multiple numbers |
| Canonicality enforcement | Always validates canonical form on parse (re-encodes and compares); explicit parse errors | Canonicality is a user concern in general use (you must re-encode and compare if needed) |
| Usage pattern | Treat `Sqid` as a first-class domain type in web models/controllers | Treat encoder as a service to transform numbers<->strings |

If you only need core encoding/decoding (not ASP.NET Core binding/JSON/OpenAPI), use [sqids-dotnet](https://github.com/sqids/sqids-dotnet). If you're building an ASP.NET Core app and want a `Sqid` value type that "just works" with model binding, JSON, and OpenAPI, this library is a better fit.

## References

- [Sqids.org](https://sqids.org) – original Sqids library and documentation
- [Sqids Specification](https://github.com/sqids/sqids-spec) – the canonical Sqids specification this implementation follows
- [sqids-dotnet](https://github.com/sqids/sqids-dotnet) – core .NET implementation of Sqids
- [Sqids GitHub Organization](https://github.com/sqids) – official implementations across languages

## Installation

Add the package to your project:

```bash
dotnet add package Atulin.Sqids.AspNetCore
```

## Quick Start

Register Sqids and (optionally) OpenAPI support in `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSqids(options =>
{
    options.MinLength = 8; // pad IDs to at least this length
    // options.Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
    // options.Blocklist.Add("badword");
});

builder.Services.AddOpenApi(o => o.AddSqidSchema());

var app = builder.Build();

app.MapOpenApi();
app.Run();
```

> [!WARNING]
> `AddSqids()` must be called before the first `Sqid` is used (the library uses a process-wide default codec). Calling it again after first use throws.

## Usage

### The `Sqid` type

`Sqid` is a `readonly struct` wrapping a non-negative `long`. It supports equality, ordering (numeric), parsing, and formatting. Parsing accepts only the canonical form produced by encoding the same value with the same alphabet, `MinLength`, and blocklist rules.

```csharp
// Create from a number
Sqid id = Sqid.From(42);
Console.WriteLine(id.ToString()); // e.g. "Lk"

// Parse
Sqid parsed = Sqid.Parse("Lk");
bool ok = Sqid.TryParse("Lk", out var value);

// Span/UTF-8 overloads are also supported
Sqid.Parse("Lk".AsSpan());
Sqid.TryParse("Lk"u8, out _);
```

### Dependency Injection: `SqidCodec`

`AddSqids()` registers the process-wide codec as a singleton `SqidCodec`, so you can inject it for programmatic encoding/decoding:

```csharp
app.MapGet("/encode/{n:long}", (long n, SqidCodec codec) => codec.Encode(n));
app.MapGet("/decode/{id}", (string id, SqidCodec codec) =>
    codec.TryDecode(id, out var v) == SqidParseError.None
        ? Results.Ok(v)
        : Results.BadRequest("Invalid id"));
```

### Minimal APIs

`Sqid` binds directly from route, query, or body parameters:

```csharp
// Route binding
app.MapGet("/users/{id:sqid}", (Sqid id) => $"User {id.Value}");

// Query binding
app.MapGet("/users", (Sqid? id) => id is null ? "All" : $"User {id.Value}");

// JSON body binding (works automatically via the built-in JsonConverter)
app.MapPost("/users", (UserDto user) => Results.Ok(user));
```

### MVC Controllers & Razor Pages

`Sqid` works as an action parameter, model property, or route constraint. The included model binder, `TypeConverter`, and `{id:sqid}` route constraint are registered automatically when you call `AddSqids()`.

```csharp
public class User
{
    public Sqid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class UsersController : Controller
{
    [HttpGet("{id:sqid}")]
    public IActionResult Get(Sqid id) => Ok(id);
}
```

### JSON Serialization

`Sqid` is serialized as a string and deserialized from a string only. Dictionary keys are also supported. Numbers are rejected during deserialization.

```csharp
var user = new User { Id = Sqid.From(123), Name = "Alice" };
string json = JsonSerializer.Serialize(user); // {"Id":"Uk","Name":"Alice"}
var restored = JsonSerializer.Deserialize<User>(json)!;
```

### OpenAPI

Call `AddSqidSchema()` when configuring OpenAPI to add a reusable `Sqid` component schema with proper constraints (`type: string`, `format: sqid`, `pattern: ^[0-9A-Za-z]+$`, `minLength: 2`, `maxLength: 12`, description, and example). All `Sqid` and `Sqid?` properties in your models will reference the shared component, and route/query/header parameters of type `Sqid` (Minimal APIs and MVC alike) are described with the same constraints inline.

```csharp
builder.Services.AddOpenApi(o => o.AddSqidSchema());

// Then serve the document as normal
app.MapOpenApi();
```

## Configuration

`SqidOptions` lets you customize the codec:

| Option | Default | Description |
|---|---|---|
| `Alphabet` | Full alphanumeric set (0-9A-Za-z) | The character set used to encode/decode. Must contain 3–94 distinct visible ASCII characters. |
| `MinLength` | `0` | If greater than 0, IDs are padded to at least this length (with padding chars chosen per the spec). |
| `Blocklist` | Official Sqids blocklist | A list of words/strings to avoid appearing in generated IDs (checked at start, end, and anywhere). The implementation only checks words the current alphabet can actually spell. |

```csharp
builder.Services.AddSqids(o => {
    o.MinLength = 10;
    o.Alphabet = "abcdef0123456789";
    o.Blocklist = new HashSet<string> { "abc", "def" };
});
```

## Synthetic Benchmark

* `SqidsNet` -> `Sqids.NET` library
* `SquidCodec` -> this library, raw codec performance

<!-- description start -->
```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
AMD Ryzen 9 9900X 4.40GHz, 1 CPU, 24 logical and 12 physical cores
.NET SDK 11.0.100-rc.2.26504.105
  [Host]     : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v4
```
<!-- description end -->
<!-- table start -->
| Method    | Value               | MinLength | Mean          | Error       | StdDev        | Median        | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|---------- |-------------------- |---------- |--------------:|------------:|--------------:|--------------:|------:|--------:|-----:|-------:|----------:|------------:|
| SqidsNet  | 1                   | 0         |    224.248 ns |  13.9751 ns |    41.2059 ns |    205.380 ns |  1.03 |    0.25 |    2 | 0.0157 |     264 B |        1.00 |
| SqidCodec | 1                   | 0         |      7.430 ns |   0.1779 ns |     0.3753 ns |      7.372 ns |  0.03 |    0.01 |    1 | 0.0019 |      32 B |        0.12 |
|           |                     |           |               |             |               |               |       |         |      |        |           |             |
| SqidsNet  | 1                   | 8         |  9,370.275 ns | 158.0572 ns |   264.0782 ns |  9,366.051 ns | 1.001 |    0.04 |    2 | 0.9155 |   15472 B |       1.000 |
| SqidCodec | 1                   | 8         |     20.401 ns |   0.4332 ns |     0.7811 ns |     20.163 ns | 0.002 |    0.00 |    1 | 0.0024 |      40 B |       0.003 |
|           |                     |           |               |             |               |               |       |         |      |        |           |             |
| SqidsNet  | 123456              | 0         |  1,480.146 ns |  29.1828 ns |    34.7400 ns |  1,474.041 ns |  1.00 |    0.03 |    2 | 0.1717 |    2896 B |        1.00 |
| SqidCodec | 123456              | 0         |     17.770 ns |   1.0463 ns |     3.0850 ns |     16.399 ns |  0.01 |    0.00 |    1 | 0.0019 |      32 B |        0.01 |
|           |                     |           |               |             |               |               |       |         |      |        |           |             |
| SqidsNet  | 123456              | 8         | 11,904.254 ns | 486.4056 ns | 1,434.1779 ns | 12,274.207 ns | 1.017 |    0.19 |    2 | 0.9155 |   15480 B |       1.000 |
| SqidCodec | 123456              | 8         |     25.288 ns |   1.3742 ns |     4.0518 ns |     22.775 ns | 0.002 |    0.00 |    1 | 0.0024 |      40 B |       0.003 |
|           |                     |           |               |             |               |               |       |         |      |        |           |             |
| SqidsNet  | 4294967296          | 0         |  5,539.387 ns |  81.7319 ns |   195.8245 ns |  5,513.649 ns | 1.001 |    0.05 |    2 | 0.6943 |   11680 B |       1.000 |
| SqidCodec | 4294967296          | 0         |     26.707 ns |   0.5267 ns |     0.4669 ns |     26.706 ns | 0.005 |    0.00 |    1 | 0.0024 |      40 B |       0.003 |
|           |                     |           |               |             |               |               |       |         |      |        |           |             |
| SqidsNet  | 4294967296          | 8         | 10,108.399 ns | 631.6650 ns | 1,862.4786 ns |  8,845.481 ns | 1.032 |    0.26 |    2 | 0.9155 |   15488 B |       1.000 |
| SqidCodec | 4294967296          | 8         |     28.775 ns |   0.5986 ns |     0.5307 ns |     28.825 ns | 0.003 |    0.00 |    1 | 0.0024 |      40 B |       0.003 |
|           |                     |           |               |             |               |               |       |         |      |        |           |             |
| SqidsNet  | 9223372036854775807 | 0         | 10,934.390 ns | 201.8682 ns |   269.4882 ns | 10,936.725 ns | 1.001 |    0.03 |    2 | 1.0681 |   18096 B |       1.000 |
| SqidCodec | 9223372036854775807 | 0         |     47.442 ns |   0.8319 ns |     0.7374 ns |     47.453 ns | 0.004 |    0.00 |    1 | 0.0029 |      48 B |       0.003 |
|           |                     |           |               |             |               |               |       |         |      |        |           |             |
| SqidsNet  | 9223372036854775807 | 8         | 11,047.489 ns | 220.4561 ns |   206.2148 ns | 11,110.414 ns | 1.000 |    0.03 |    2 | 1.0681 |   18096 B |       1.000 |
| SqidCodec | 9223372036854775807 | 8         |     48.201 ns |   0.6385 ns |     0.5332 ns |     48.325 ns | 0.004 |    0.00 |    1 | 0.0029 |      48 B |       0.003 |
<!-- table end -->

## Notes on Canonicality & Safety

- **Canonical form required**: Parsing/accepting an ID re-encodes its decoded value and requires the result to match exactly. This rejects non-canonical forms such as leading-zero-like artifacts, wrong padding, IDs produced with different options, blocked IDs, or IDs that do not represent a single value.
- **Single-value only**: The library encodes and decodes a single integer per ID (matching the Sqids design). Multi-number encodings are rejected on parse with a distinct parse error.
- **Not encryption or obfuscation of sensitive data**: Sqids are intended for public, non-sensitive identifiers. Do not rely on them for security.
- **Blocklist aware**: When a generated ID would contain a blocked word, the encoder retries with a different permutation until it finds a safe ID or exhausts attempts.

## Building & Testing

This project targets `net10.0`. Tests use [TUnit](https://tunit.dev) on Microsoft.Testing.Platform.

```bash
# Build everything
dotnet build

# Run tests
dotnet test
```

## License

[MIT License](LICENSE)
