using System.Text;
using Sqids;

namespace Sqids.AspNetCore.Tests;

public sealed class CodecDecodingTests
{
	private static SqidCodec Default => new();

	[Test]
	public async Task Decode_Empty_ReturnsEmpty()
	{
		await Assert.That(Default.TryDecode("", out _)).IsEqualTo(SqidParseError.Empty);
	}

	[Test]
	[Arguments("a")]
	[Arguments("b")]
	[Arguments("aaaaaaaaaaaaa")]
	public async Task Decode_WrongLength_ReturnsInvalidLength(string id)
	{
		await Assert.That(Default.TryDecode(id, out _)).IsEqualTo(SqidParseError.InvalidLength);
	}

	[Test]
	[Arguments("**")]
	[Arguments("$$")]
	[Arguments("bM!")]
	public async Task Decode_NonAlphabetCharacter_ReturnsInvalidCharacter(string id)
	{
		await Assert.That(Default.TryDecode(id, out _)).IsEqualTo(SqidParseError.InvalidCharacter);
	}

	[Test]
	[Arguments("aaaaaaaaaaaa")]
	[Arguments("zzzzzzzzzzzz")]
	public async Task Decode_ValueBeyondLong_ReturnsOverflow(string id)
	{
		await Assert.That(Default.TryDecode(id, out _)).IsEqualTo(SqidParseError.Overflow);
	}

	[Test]
	[Arguments("UkX")]
	[Arguments("UkXy")]
	[Arguments("aho1e")]
	public async Task Decode_PaddedOrNonCanonical_ReturnsNotCanonical(string id)
	{
		await Assert.That(Default.TryDecode(id, out _)).IsEqualTo(SqidParseError.NotCanonical);
	}

	[Test]
	public async Task Decode_PaddedIdFromLongerMinLength_ReturnsNotCanonical()
	{
		var padded = new SqidCodec(new SqidOptions { MinLength = 6 }).Encode(1);

		await Assert.That(padded).IsEqualTo("UkLWZg");
		await Assert.That(Default.TryDecode(padded, out _)).IsEqualTo(SqidParseError.NotCanonical);
	}

	[Test]
	public async Task Decode_IdFromDifferentAlphabet_ReturnsNotCanonical()
	{
		var other = new SqidCodec(new SqidOptions { Alphabet = "fedcba" });
		var id = new SqidCodec(new SqidOptions { Alphabet = "abcdef" }).Encode(12345);

		await Assert.That(other.TryDecode(id, out _)).IsEqualTo(SqidParseError.NotCanonical);
	}

	[Test]
	[MethodDataSource(typeof(ReferenceVectors), nameof(ReferenceVectors.MultiNumberIds))]
	public async Task Decode_MultiNumberId_ReturnsMultipleValues(string id)
	{
		await Assert.That(Default.TryDecode(id, out _)).IsEqualTo(SqidParseError.MultipleValues);
	}

	[Test]
	public async Task Decode_SeparatorAsFirstDigit_ReturnsMalformed()
	{
		var codec = new SqidCodec(new SqidOptions { Alphabet = "abc" });

		await Assert.That(codec.TryDecode("ac", out _)).IsEqualTo(SqidParseError.Malformed);
		await Assert.That(codec.TryDecode("ba", out _)).IsEqualTo(SqidParseError.Malformed);
		await Assert.That(codec.TryDecode("cb", out _)).IsEqualTo(SqidParseError.Malformed);
	}

	[Test]
	public async Task Decode_ShortMinLengthCodec_HandlesNonCanonicalAndMultiValue()
	{
		var codec = new SqidCodec(new SqidOptions { Alphabet = "abc", MinLength = 3 });
		var id = codec.Encode(1);

		await Assert.That(codec.TryDecode(id, out var value)).IsEqualTo(SqidParseError.None);
		await Assert.That(value).IsEqualTo(1L);
		await Assert.That(codec.TryDecode("aaca", out _)).IsEqualTo(SqidParseError.MultipleValues);
		await Assert.That(codec.TryDecode("caaa", out _)).IsEqualTo(SqidParseError.NotCanonical);
	}

	[Test]
	public async Task Decode_LongCodec_NoPaddingBranch()
	{
		var codec = new SqidCodec(new SqidOptions { Alphabet = "abc", MinLength = 40 });
		var id = codec.Encode(1);

		await Assert.That(id.Length).IsGreaterThanOrEqualTo(40);
		await Assert.That(codec.TryDecode(id, out var value)).IsEqualTo(SqidParseError.None);
		await Assert.That(value).IsEqualTo(1L);
	}

	[Test]
	public async Task Decode_Span_MatchesStringOverload()
	{
		var codec = Default;

		await Assert.That(codec.TryDecode("bM".AsSpan(), out var value)).IsEqualTo(SqidParseError.None);
		await Assert.That(value).IsEqualTo(0L);
		await Assert.That(codec.TryDecode("a".AsSpan(), out _)).IsEqualTo(SqidParseError.InvalidLength);
		await Assert.That(codec.TryDecode("**".AsSpan(), out _)).IsEqualTo(SqidParseError.InvalidCharacter);
		await Assert.That(codec.TryDecode("UkX".AsSpan(), out _)).IsEqualTo(SqidParseError.NotCanonical);
	}

	[Test]
	public async Task Decode_Utf8_MatchesStringOverload()
	{
		var codec = Default;
		var bytes = Encoding.UTF8.GetBytes("bM");

		await Assert.That(codec.TryDecode(bytes, out var value)).IsEqualTo(SqidParseError.None);
		await Assert.That(value).IsEqualTo(0L);
		await Assert.That(codec.TryDecode(Array.Empty<byte>(), out _)).IsEqualTo(SqidParseError.Empty);
		await Assert.That(codec.TryDecode("a"u8, out _)).IsEqualTo(SqidParseError.InvalidLength);
		await Assert.That(codec.TryDecode("**"u8, out _)).IsEqualTo(SqidParseError.InvalidCharacter);
	}

	[Test]
	public async Task Decode_MaxLengthId_RoundTrips()
	{
		var codec = Default;
		var id = codec.Encode(long.MaxValue);

		await Assert.That(id.Length).IsEqualTo(12);
		await Assert.That(codec.TryDecode(id, out var value)).IsEqualTo(SqidParseError.None);
		await Assert.That(value).IsEqualTo(long.MaxValue);
	}
}
