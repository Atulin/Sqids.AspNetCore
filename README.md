# Sqids.AspNetCore

[![Coverage Status](https://coveralls.io/repos/github/Atulin/Sqids.AspNetCore/badge.svg?branch=master)](https://coveralls.io/github/Atulin/Sqids.AspNetCore?branch=master)
[![Tests & Coverage](https://github.com/Atulin/Sqids.AspNetCore/actions/workflows/coverage.yml/badge.svg)](https://github.com/Atulin/Sqids.AspNetCore/actions/workflows/coverage.yml)

A strongly-typed `Sqid` for ASP.NET Core, built on the [Sqids](https://sqids.org) ID generation scheme. It encodes non-negative integers into short, URL-safe, non-sequential IDs and decodes them back.

This library provides first-class integration with ASP.NET Core: route/query/form/model binding for Minimal APIs, MVC, and Razor Pages; JSON serialization/deserialization via a built-in `JsonConverter`; and OpenAPI schema generation. IDs are validated to be in canonical form, matching the behavior of other official Sqids implementations.

## References

- [Sqids.org](https://sqids.org) – original Sqids library and documentation
- [Sqids Specification](https://github.com/sqids/sqids-spec) – the canonical Sqids specification this implementation follows
- [Sqids GitHub Organization](https://github.com/sqids) – official implementations across languages

## Installation

Add the package to your project:

```bash
dotnet add package Sqids.AspNetCore
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

### Dependency Injection: `ISqidCodec`

For programmatic encoding/decoding with custom options or to control the codec instance, inject `ISqidCodec`:

```csharp
app.MapGet("/encode/{n:long}", (long n, ISqidCodec codec) => codec.Encode(n));
app.MapGet("/decode/{id}", (string id, ISqidCodec codec) =>
    codec.TryDecode(id, out var v) ? Results.Ok(v) : Results.BadRequest("Invalid id"));
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

Call `AddSqidSchema()` when configuring OpenAPI to add a reusable `Sqid` component schema with proper constraints (`type: string`, `format: sqid`, `pattern: ^[0-9A-Za-z]+$`, `minLength: 2`, `maxLength: 12`, description, and example). All `Sqid` and `Sqid?` properties in your models will reference the shared component.

```csharp
builder.Services.AddOpenApi(o => o.AddSqidSchema());

// Then document endpoints as normal
app.MapGet("/openapi/v1.json", () => ...);
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


