# Sqids.AspNetCore.Demo

A demonstration project showcasing how to use [Sqids.AspNetCore](../README.md).

## What it demonstrates

| Feature | Where |
|---|---|
| `AddSqids()` registration with custom options (`MinLength = 6`) | `Program.cs` |
| Injected `SqidCodec` for programmatic encode/decode | `GET /encode/{n:long}`, `GET /decode/{id}` |
| Minimal API binding + `{id:sqid}` route constraint | `GET /users/mini/{id:sqid}` |
| MVC controller binding from route, query and JSON body | `Controllers/UsersController.cs` |
| JSON serialization of `Sqid` properties via the built-in `JsonConverter` | `POST /echo` |
| OpenAPI schema generation via `AddSqidSchema()` | `/openapi/v1.json`, `/scalar/v1` |
| Razor Pages with the `{id:sqid}` route constraint | `Pages/Details.cshtml` |

The demo configures `MinLength = 6`, so every ID is padded to at least six characters
(e.g. `1` → `UkLWZg`). Because parsing is strict, only these padded forms are accepted.

## Running the demo

```bash
dotnet run --project Sqids.AspNetCore.Demo
```

Then open:

- `http://localhost:5000/` — home page with links and an endpoint overview
- `http://localhost:5000/scalar/v1` — interactive API documentation (Scalar)
- `http://localhost:5000/openapi/v1.json` — the generated OpenAPI document

## Example calls

```bash
# Encode a number (42 -> "JgaEBg" with MinLength = 6)
curl http://localhost:5000/encode/42
# {"number":42,"sqid":"JgaEBg"}

# Decode a Sqid (non-canonical text is rejected with 400)
curl http://localhost:5000/decode/JgaEBg
# {"sqid":"JgaEBg","number":42}

# Minimal API: the {id:sqid} route constraint returns 404 for non-canonical IDs
curl http://localhost:5000/users/mini/JgaEBg

# MVC: JSON body round-trip — Sqid properties serialize as strings
curl -X POST http://localhost:5000/echo \
  -H "Content-Type: application/json" \
  -d '{"id":"UkLWZg","name":"Alice","email":"alice@example.com"}'
# {"id":"UkLWZg","name":"Alice","email":"alice@example.com"}

# MVC: create a user; the response Location header contains the new Sqid
curl -i -X POST http://localhost:5000/api/users \
  -H "Content-Type: application/json" \
  -d '{"name":"Jane Doe","email":"jane@example.com"}'

# MVC: nullable Sqid from the query string
curl "http://localhost:5000/api/users/by-sqid?id=UkLWZg"
```
