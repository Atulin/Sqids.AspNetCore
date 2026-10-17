using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Sqids.AspNetCore.Tests;

internal sealed record OpenApiThing(Sqid Id, Sqid? Optional, int Number);

public sealed class SqidOpenApiTests
{
	private static readonly MethodInfo CharacterClassMethod =
		typeof(SqidOpenApiExtensions).GetMethod("CharacterClass", BindingFlags.NonPublic | BindingFlags.Static)!;

	[Test]
	public async Task Schema_DescribesSqidProperties()
	{
		var document = await GenerateDocumentAsync();

		var id = GetSchema(document, "Sqid");
		await Assert.That(id["type"]!.GetValue<string>()).IsEqualTo("string");
		await Assert.That(id["format"]!.GetValue<string>()).IsEqualTo("sqid");
		await Assert.That(id["pattern"]!.GetValue<string>()).IsEqualTo("^[0-9A-Za-z]+$");
		await Assert.That(id["minLength"]!.GetValue<int>()).IsEqualTo(2);
		await Assert.That(id["maxLength"]!.GetValue<int>()).IsEqualTo(12);
		await Assert.That(id["description"]!.GetValue<string>()).IsEqualTo("Opaque identifier. Only the canonical form is accepted.");
		await Assert.That(id["examples"]![0]!.GetValue<string>()).IsEqualTo("Uk");

		var thing = document["components"]?["schemas"]?["OpenApiThing"]?.AsObject();
		var optional = GetSchemaRef(document, thing, "optional");
		await Assert.That(optional["format"]!.GetValue<string>()).IsEqualTo("sqid");

		var number = GetSchemaRef(document, thing, "number");
		// The int schema may be written as a single type or as the OpenAPI 3.1 type array ["integer","string"].
		await Assert.That(ContainsType(number, "integer")).IsTrue();
	}

	[Test]
	public async Task Schema_UsesOneSharedComponentForEverySqidProperty()
	{
		var document = await GenerateDocumentAsync();
		var thing = document["components"]?["schemas"]?["OpenApiThing"]!;

		// Both the required and the nullable property point at the very same component schema.
		await Assert.That(thing["properties"]!["id"]!["$ref"]!.GetValue<string>())
			.IsEqualTo("#/components/schemas/Sqid");
		await Assert.That(thing["properties"]!["optional"]!["oneOf"]![1]!["$ref"]!.GetValue<string>())
			.IsEqualTo("#/components/schemas/Sqid");
	}

	private static bool ContainsType(JsonObject schema, string type)
	{
		return schema["type"] switch
		{
			JsonValue value => value.GetValue<string>() == type,
			JsonArray array => array.Any(n => n is JsonValue v && v.GetValue<string>() == type),
			_ => false,
		};
	}

	private static async Task<JsonNode> GenerateDocumentAsync()
	{
		var builder = WebApplication.CreateBuilder();
		builder.WebHost.UseTestServer();
		builder.Services.AddOpenApi(options => options.AddSqidSchema());

		await using var app = builder.Build();
		app.MapOpenApi();
		app.MapPost("/thing", (OpenApiThing thing) => thing);
		await app.StartAsync();

		var client = app.GetTestClient();
		return JsonNode.Parse(await client.GetStringAsync("/openapi/v1.json"))!;
	}

	private static JsonObject GetSchema(JsonNode document, string schemaName)
	{
		return document["components"]?["schemas"]?[schemaName]?.AsObject()
			?? throw new InvalidOperationException($"Schema '{schemaName}' not found.");
	}

	private static JsonObject GetSchemaRef(JsonNode document, JsonObject? schema, string propertyName)
	{
		if (schema is null)
		{
			throw new InvalidOperationException("Schema not found.");
		}

		// A nullable `Sqid?` is wrapped in a `oneOf` whose first branch is `type: null`, so prefer the sqid one.
		JsonObject? first = null;
		foreach (var candidate in Candidates(document, document["components"]?["schemas"]?.AsObject(), schema["properties"]?[propertyName]))
		{
			first ??= candidate;
			if (candidate["format"]?.GetValue<string>() == "sqid")
			{
				return candidate;
			}
		}

		return first ?? throw new InvalidOperationException($"No property named '{propertyName}' was found.");
	}

	private static IEnumerable<JsonObject> Candidates(JsonNode document, JsonObject? schemas, JsonNode? node)
	{
		while (node is JsonObject obj)
		{
			if (obj["$ref"]?.GetValue<string>() is not { } reference)
			{
				if (obj["oneOf"]?.AsArray() is not { } alternatives)
				{
					yield return obj;
					yield break;
				}

				foreach (var alternative in alternatives)
				{
					foreach (var candidate in Candidates(document, schemas, alternative))
					{
						yield return candidate;
					}
				}

				yield break;
			}

			if (schemas is null)
			{
				yield break;
			}

			var name = reference[(reference.LastIndexOf('/') + 1)..];
			node = schemas[name];
		}
	}

	[Test]
	[Arguments("]", "\\]")]
	[Arguments("^", "\\^")]
	[Arguments("\\", "\\\\")]
	[Arguments("-", "\\-")]
	[Arguments("z]", "z\\]")]
	[Arguments("abc", "a-c")]
	[Arguments("ab", "ab")]
	[Arguments("a", "a")]
	[Arguments("abcxyz", "a-cx-z")]
	[Arguments("---", "\\-\\-\\-")]
	public async Task CharacterClass_EscapesAndCompressesRuns(string input, string expected)
	{
		await Assert.That(InvokeCharacterClass(input)).IsEqualTo(expected);
	}

	[Test]
	public async Task CharacterClass_DefaultAlphabetIsTheSortedSet()
	{
		var sorted = new SqidCodec().AlphabetCharacters;
		await Assert.That(InvokeCharacterClass(sorted)).IsEqualTo("0-9A-Za-z");
	}

	private static string InvokeCharacterClass(string sortedChars) =>
		(string)CharacterClassMethod.Invoke(null, [sortedChars])!;
}
