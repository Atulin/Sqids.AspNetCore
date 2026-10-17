# Sqids.AspNetCore

A `Sqid` type for ASP.NET Core built on the [Sqids](https://sqids.org) spec. It binds route,
query, form and JSON-body values in Minimal APIs, MVC and Razor Pages, serialises through a
`JsonConverter`, and describes itself in OpenAPI documents via `options.AddSqidSchema()`.

```csharp
builder.Services.AddSqids(o => o.MinLength = 8);   // before the first Sqid is used
builder.Services.AddOpenApi(o => o.AddSqidSchema());
```

See `PROMPT.md` for the design notes.

## Tests

The test suite uses [TUnit](https://tunit.dev) on the Microsoft.Testing.Platform. Because the
projects target `net10.0` while newer SDKs ship a newer shared runtime, either run the tests from
an IDE, or build and run the test executable directly:

```bash
dotnet build Sqids.AspNetCore.Tests
DOTNET_ROLL_FORWARD=LatestMajor ./Sqids.AspNetCore.Tests/bin/Debug/net10.0/Sqids.AspNetCore.Tests
```

## Code coverage

Every hand-written line of `Sqids.AspNetCore` is covered by the tests. The only code excluded from
coverage reports is compiler/source-generated code that is compiled into the library but can never
run from the test suite; the exclusion lives in `Sqids.AspNetCore.Tests/coverage.settings.xml`.

To collect a Cobertura report, pass the coverage options to the test executable (each option is
prefixed with two hyphens): `coverage`, `coverage-output-format cobertura`, `coverage-output
<file>`, and `coverage-settings Sqids.AspNetCore.Tests/coverage.settings.xml`. For example:

```bash
dotnet build Sqids.AspNetCore.Tests
cd Sqids.AspNetCore.Tests/bin/Debug/net10.0
DOTNET_ROLL_FORWARD=LatestMajor ./Sqids.AspNetCore.Tests \
  --coverage --coverage-output-format cobertura \
  --coverage-output coverage.cobertura.xml \
  --coverage-settings coverage.settings.xml
```
