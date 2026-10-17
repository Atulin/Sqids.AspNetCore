using Sqids;

namespace Sqids.AspNetCore.Tests;

public sealed class CodecEncodingTests
{
	[Test]
	[MethodDataSource(typeof(ReferenceVectors), nameof(ReferenceVectors.EncodeVectors))]
	public async Task Encode_MatchesReferenceImplementation(EncodeVector vector)
	{
		var codec = vector.CreateCodec();
		var actual = codec.Encode(vector.Value);

		await Assert.That(actual).IsEqualTo(vector.Expected);
	}

	[Test]
	[MethodDataSource(typeof(ReferenceVectors), nameof(ReferenceVectors.EncodeVectors))]
	public async Task Decode_ReferenceId_RoundTrips(EncodeVector vector)
	{
		var codec = vector.CreateCodec();
		var error = codec.TryDecode(vector.Expected!, out var value);

		await Assert.That(error).IsEqualTo(SqidParseError.None);
		await Assert.That(value).IsEqualTo(vector.Value);
	}

	[Test]
	[MethodDataSource(typeof(ReferenceVectors), nameof(ReferenceVectors.ThrowVectors))]
	public async Task Encode_WhenEveryOffsetIsBlocked_Throws(ThrowVector vector)
	{
		var codec = ReferenceVectors.CreateCodec(vector.Alphabet, vector.MinLength, vector.Blocklist);

		await Assert.That(vector.Throws).IsTrue();
		await Assert.That(() => codec.Encode(vector.Value)).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task Encode_DefaultCodec_IncrementalNumbers()
	{
		var codec = new SqidCodec();

		string[] expected = ["bM", "Uk", "gb", "Ef", "Vq", "uw", "OI", "AX", "p6", "nJ"];
		for (var i = 0; i < expected.Length; i++)
		{
			await Assert.That(codec.Encode(i)).IsEqualTo(expected[i]);
		}
	}

	[Test]
	public async Task Encode_EmptyBlocklist_ProducesBlockedWord()
	{
		var codec = ReferenceVectors.CreateCodec(null, 0, "none");

		await Assert.That(codec.Encode(4572721)).IsEqualTo("aho1e");
		await Assert.That(codec.TryDecode("aho1e", out var value)).IsEqualTo(SqidParseError.None);
		await Assert.That(value).IsEqualTo(4572721L);
	}

	[Test]
	public async Task Encode_NegativeValue_Throws()
	{
		var codec = new SqidCodec();

		await Assert.That(() => codec.Encode(-1)).Throws<ArgumentOutOfRangeException>();
	}

	[Test]
	public async Task Encode_LongMaxValue_IsLongestIdAndRoundTrips()
	{
		var codec = new SqidCodec();
		var id = codec.Encode(long.MaxValue);

		await Assert.That(id.Length).IsEqualTo(codec.MaxIdLength);
		await Assert.That(codec.TryDecode(id, out var value)).IsEqualTo(SqidParseError.None);
		await Assert.That(value).IsEqualTo(long.MaxValue);
	}

	[Test]
	public async Task Encode_ValuesAboveUIntMax_RoundTrip()
	{
		var codec = new SqidCodec();

		foreach (var value in new[] { (long)uint.MaxValue, (long)uint.MaxValue + 1, 5_000_000_000, long.MaxValue - 1 })
		{
			var id = codec.Encode(value);
			await Assert.That(codec.TryDecode(id, out var decoded)).IsEqualTo(SqidParseError.None);
			await Assert.That(decoded).IsEqualTo(value);
		}
	}

	[Test]
	public async Task Encode_FirstThousandValues_RoundTrip()
	{
		var codec = new SqidCodec();

		for (var value = 0L; value < 1000; value++)
		{
			var id = codec.Encode(value);
			var error = codec.TryDecode(id, out var decoded);
			if (error != SqidParseError.None || decoded != value)
			{
				await Assert.That(error).IsEqualTo(SqidParseError.None);
				await Assert.That(decoded).IsEqualTo(value);
			}
		}

		await Assert.That(codec.Encode(999)).IsNotNull();
	}
}
