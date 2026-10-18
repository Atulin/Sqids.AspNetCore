# Sqids.AspNetCore.Benchmarks

Benchmarks comparing `SqidCodec` (this library) against [Sqids.NET](https://www.nuget.org/packages/Sqids)
— the official .NET port of the Sqids spec — for the single-number encode/decode scenario this library
is specialised for.

## What is measured

| Class | Methods |
| --- | --- |
| `EncodeBenchmarks` | `SqidsEncoder<long>.Encode(long)` vs `SqidCodec.Encode(long)` |
| `DecodeBenchmarks` | `SqidsEncoder<long>.Decode(string)` vs `SqidCodec.TryDecode(string, out long)` and `Sqid.TryParse` (strict public API with canonical-form validation) |

Each benchmark runs over several value sizes (`1`, `123456`, `2^32`, `long.MaxValue`) and two
`MinLength` settings (`0` = default, `8` = padded), and reports allocations via `[MemoryDiagnoser]`.

Both sides use the same default alphabet and blocklist. Every benchmark class verifies at setup time
that the two implementations produce byte-for-byte identical IDs for the given input — if they ever
disagree, the benchmark fails loudly instead of measuring nonsense.

## Running

```bash
# Everything (Release is required for meaningful numbers)
dotnet run -c Release --project Sqids.AspNetCore.Benchmarks

# A subset
dotnet run -c Release --project Sqids.AspNetCore.Benchmarks -- --filter *Encode*

# Quick smoke run
dotnet run -c Release --project Sqids.AspNetCore.Benchmarks -- --filter * --job short
```
