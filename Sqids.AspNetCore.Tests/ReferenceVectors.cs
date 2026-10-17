using System.Text.Json;

namespace Sqids.AspNetCore.Tests;

/// <summary>One encode/round-trip vector generated from the official Python Sqids reference.</summary>
public sealed class EncodeVector
{
	public string Name { get; set; } = "";
	public string? Alphabet { get; set; }
	public int MinLength { get; set; }

	/// <summary>"default" = the official blocklist, "none" = disabled, otherwise words joined by '|'.</summary>
	public string Blocklist { get; set; } = "default";

	public long Value { get; set; }
	public string? Expected { get; set; }
	public string? Error { get; set; }

	public SqidCodec CreateCodec() => ReferenceVectors.CreateCodec(Alphabet, MinLength, Blocklist);

	public override string ToString() => $"{Name}[n={Value}] -> {Expected ?? "<error>"}";
}

public sealed class ThrowVector
{
	public string Name { get; set; } = "";
	public string? Alphabet { get; set; }
	public int MinLength { get; set; }
	public string Blocklist { get; set; } = "default";
	public long Value { get; set; }
	public bool Throws { get; set; }

	public override string ToString() => $"{Name}[n={Value}] throws={Throws}";
}

internal sealed class VectorFile
{
	public List<EncodeVector> Encode { get; set; } = [];
	public List<ThrowVector> Throws { get; set; } = [];
	public Dictionary<string, long[]> Multi { get; set; } = [];
}

/// <summary>
/// The official Sqids test vectors for single-number IDs, produced by the reference Python implementation.
/// Multi-number IDs are included only as decode inputs: this library encodes exactly one number per ID.
/// </summary>
internal static class ReferenceVectors
{
	private static readonly VectorFile Data;

	public static IEnumerable<Func<EncodeVector>> EncodeVectors()
		=> Data.Encode.Select(static v => (Func<EncodeVector>)(() => v));

	public static IEnumerable<Func<ThrowVector>> ThrowVectors()
		=> Data.Throws.Select(static v => (Func<ThrowVector>)(() => v));

	public static IEnumerable<Func<string>> MultiNumberIds()
		=> Data.Multi.Keys.Select(static id => (Func<string>)(() => id));

	static ReferenceVectors()
	{
		var assembly = typeof(ReferenceVectors).Assembly;
		var resourceName = assembly
			.GetManifestResourceNames()
			.Single(static n => n.EndsWith("reference_vectors.json", StringComparison.Ordinal));

		using var stream = assembly.GetManifestResourceStream(resourceName)!;
		Data = JsonSerializer.Deserialize<VectorFile>(stream, new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true,
		})!;
	}

	public static SqidCodec CreateCodec(string? alphabet, int minLength, string blocklist)
	{
		var options = new SqidOptions
		{
			MinLength = minLength,
			Blocklist = blocklist switch
			{
				"default" => null,
				"none" => [],
				_ => blocklist.Split('|'),
			},
		};

		if (alphabet is not null)
		{
			options.Alphabet = alphabet;
		}

		return new SqidCodec(options);
	}
}

