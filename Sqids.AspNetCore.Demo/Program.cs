using Scalar.AspNetCore;
using Sqids.AspNetCore;
using Sqids.AspNetCore.Demo;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSqids(options =>
{
    options.MinLength = 6;
});

builder.Services.AddOpenApi(o => o.AddSqidSchema());
builder.Services.AddControllers();
builder.Services.AddRazorPages();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseStaticFiles();
app.MapControllers();
app.MapRazorPages();

// JSON body binding: Sqid serializes/deserializes as a string via the built-in JsonConverter,
// and the generated OpenAPI document describes it with the shared `Sqid` component schema.
app.MapPost("/echo", (UserDto dto) => Results.Ok(dto));

app.MapGet("/encode/{n:long}", (long n, SqidCodec codec) =>
    Results.Ok(new { Number = n, Sqid = codec.Encode(n) }));

app.MapGet("/decode/{id}", (string id, SqidCodec codec) =>
    codec.TryDecode(id, out var value) == SqidParseError.None
        ? Results.Ok(new { Sqid = id, Number = value })
        : Results.BadRequest(new { Error = "Invalid Sqid", Input = id }));

app.MapGet("/users/mini/{id:sqid}", (Sqid id) =>
    Results.Ok(new { Message = "Found via minimal API", Id = id, Value = id.Value }));

app.Run();
