using System.Text.Json;

namespace Sqids.AspNetCore.Tests;

internal sealed record JsonThing(Sqid Id, Sqid? Optional, int Number);

public sealed class SqidJsonConverterTests
{
	[Test]
	public async Task Serialize_WritesTheCanonicalSqidString()
	{
		var json = JsonSerializer.Serialize(new JsonThing(Sqid.From(1), null, 5));
		await Assert.That(json).IsEqualTo("""{"Id":"Uk","Optional":null,"Number":5}""");
	}

	[Test]
	public async Task Deserialize_ReadsTheCanonicalSqidString()
	{
		var thing = JsonSerializer.Deserialize<JsonThing>("""{"Id":"Uk","Optional":"Uk","Number":5}""")!;
		await Assert.That(thing).IsEqualTo(new JsonThing(Sqid.From(1), Sqid.From(1), 5));
	}

	[Test]
	public async Task Deserialize_EscapedString_UsesTheSlowPath()
	{
		var thing = JsonSerializer.Deserialize<JsonThing>("""{"Id":"\u0055k","Optional":null,"Number":1}""")!;
		await Assert.That(thing.Id).IsEqualTo(Sqid.From(1));
	}

	[Test]
	public async Task Deserialize_RejectsAJsonNumber()
	{
		var ex = TestHelpers.Capture(() =>
		{
			_ = JsonSerializer.Deserialize<JsonThing>("""{"Id":5,"Optional":null,"Number":1}""");
		});

		await Assert.That(ex).IsTypeOf<JsonException>();
		await Assert.That(ex.Message).Contains("Expected a Sqid string");
	}

	[Test]
	public async Task Deserialize_InvalidString_Throws()
	{
		var ex = TestHelpers.Capture(() =>
		{
			_ = JsonSerializer.Deserialize<JsonThing>("""{"Id":"**","Optional":null,"Number":1}""");
		});

		await Assert.That(ex).IsTypeOf<JsonException>();
		await Assert.That(ex.Message).Contains("Invalid Sqid");
	}

	[Test]
	public async Task DictionaryKeys_RoundTrip()
	{
		var map = new Dictionary<Sqid, int>
		{
			[Sqid.From(1)] = 10,
			[Sqid.From(100)] = 20,
		};

		var json = JsonSerializer.Serialize(map);
		await Assert.That(json).Contains($"\"{Sqid.From(1)}\":10");
		await Assert.That(json).Contains($"\"{Sqid.From(100)}\":20");

		var roundTripped = JsonSerializer.Deserialize<Dictionary<Sqid, int>>(json)!;
		await Assert.That(roundTripped[Sqid.From(1)]).IsEqualTo(10);
		await Assert.That(roundTripped[Sqid.From(100)]).IsEqualTo(20);
	}
}
