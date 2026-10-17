using Sqids;

namespace Sqids.AspNetCore.Tests;

public sealed class CodecOptionsTests
{
	private static string AllVisibleAscii => string.Concat(Enumerable.Range(0x21, 94).Select(static c => (char)c));

	[Test]
	public async Task Ctor_NullOptions_Throws()
	{
		await Assert.That(() => new SqidCodec(null!)).Throws<ArgumentNullException>();
	}

	[Test]
	public async Task Ctor_NullAlphabet_Throws()
	{
		await Assert.That(() => new SqidCodec(new SqidOptions { Alphabet = null! }))
			.Throws<ArgumentException>();
	}

	[Test]
	[Arguments("")]
	[Arguments("a")]
	[Arguments("ab")]
	public async Task Ctor_AlphabetTooShort_Throws(string alphabet)
	{
		await Assert.That(() => new SqidCodec(new SqidOptions { Alphabet = alphabet }))
			.Throws<ArgumentException>();
	}

	[Test]
	public async Task Ctor_AlphabetTooLong_Throws()
	{
		await Assert.That(() => new SqidCodec(new SqidOptions { Alphabet = new string('a', 95) }))
			.Throws<ArgumentException>();
	}

	[Test]
	public async Task Ctor_AlphabetWithNonVisibleChar_Throws()
	{
		await Assert.That(() => new SqidCodec(new SqidOptions { Alphabet = "ab " }))
			.Throws<ArgumentException>();
	}

	[Test]
	public async Task Ctor_AlphabetWithDuplicateChar_Throws()
	{
		await Assert.That(() => new SqidCodec(new SqidOptions { Alphabet = "aab" }))
			.Throws<ArgumentException>();
	}

	[Test]
	[Arguments(-1)]
	[Arguments(256)]
	[Arguments(1000)]
	public async Task Ctor_MinLengthOutOfRange_Throws(int minLength)
	{
		await Assert.That(() => new SqidCodec(new SqidOptions { MinLength = minLength }))
			.Throws<ArgumentException>();
	}

	[Test]
	[Arguments(0)]
	[Arguments(1)]
	[Arguments(2)]
	[Arguments(255)]
	public async Task Ctor_MinLengthInRange_Succeeds(int minLength)
	{
		var codec = new SqidCodec(new SqidOptions { MinLength = minLength });

		await Assert.That(codec.MinLength).IsEqualTo(minLength);
	}

	[Test]
	public async Task Ctor_MaxVisibleAlphabet_Succeeds()
	{
		var codec = new SqidCodec(new SqidOptions { Alphabet = AllVisibleAscii });

		await Assert.That(codec.AlphabetCharacters).IsEqualTo(AllVisibleAscii);
		await Assert.That(codec.Encode(12345).Length).IsGreaterThanOrEqualTo(2);
	}

	[Test]
	public async Task Defaults_ExposedByProperties()
	{
		var codec = new SqidCodec();

		await Assert.That(codec.MinLength).IsEqualTo(0);
		await Assert.That(codec.MinIdLength).IsEqualTo(2);
		await Assert.That(codec.MaxIdLength).IsEqualTo(12);
		await Assert.That(codec.AlphabetCharacters)
			.IsEqualTo("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz");
	}

	[Test]
	[Arguments(0, 2, 12)]
	[Arguments(1, 2, 12)]
	[Arguments(2, 2, 12)]
	[Arguments(5, 5, 12)]
	[Arguments(12, 12, 12)]
	[Arguments(13, 13, 13)]
	[Arguments(20, 20, 20)]
	[Arguments(255, 255, 255)]
	public async Task IdLengthBounds_DerivedFromMinLength(int minLength, int expectedMin, int expectedMax)
	{
		var codec = new SqidCodec(new SqidOptions { MinLength = minLength });

		await Assert.That(codec.MinIdLength).IsEqualTo(expectedMin);
		await Assert.That(codec.MaxIdLength).IsEqualTo(expectedMax);
	}

	[Test]
	public async Task AlphabetCharacters_IsSortedAndDeduplicated()
	{
		var codec = new SqidCodec(new SqidOptions { Alphabet = "cba" });

		await Assert.That(codec.AlphabetCharacters).IsEqualTo("abc");
	}

	[Test]
	public async Task Encode_MinLength_PadsToMinimum()
	{
		foreach (var minLength in new[] { 3, 4, 5, 10, 40, 255 })
		{
			var codec = new SqidCodec(new SqidOptions { MinLength = minLength });
			var id = codec.Encode(1);

			await Assert.That(id.Length).IsGreaterThanOrEqualTo(minLength);
			await Assert.That(codec.TryDecode(id, out var value)).IsEqualTo(SqidParseError.None);
			await Assert.That(value).IsEqualTo(1L);
		}
	}

	[Test]
	public async Task TryEncode_NegativeValue_ReturnsFalse()
	{
		var codec = new SqidCodec();
		var destination = new char[255];
		var utf8 = new byte[255];

		await Assert.That(codec.TryEncode(-1, destination, out var written)).IsFalse();
		await Assert.That(written).IsEqualTo(0);
		await Assert.That(codec.TryEncode(-1, utf8, out var bytes)).IsFalse();
		await Assert.That(bytes).IsEqualTo(0);
	}

	[Test]
	public async Task TryEncode_DestinationTooSmall_ReturnsFalse()
	{
		var codec = new SqidCodec();

		await Assert.That(codec.TryEncode(0, new char[1], out var written)).IsFalse();
		await Assert.That(written).IsEqualTo(0);
		await Assert.That(codec.TryEncode(0, new byte[1], out var bytes)).IsFalse();
		await Assert.That(bytes).IsEqualTo(0);
	}

	[Test]
	public async Task TryEncode_Success_WritesSameAsEncode()
	{
		var codec = new SqidCodec();
		var destination = new char[255];

		await Assert.That(codec.TryEncode(4572721, destination, out var written)).IsTrue();
		await Assert.That(new string(destination, 0, written)).IsEqualTo(codec.Encode(4572721));

		var utf8 = new byte[255];
		await Assert.That(codec.TryEncode(4572721, utf8, out var bytes)).IsTrue();
		await Assert.That(bytes).IsEqualTo(written);
		await Assert.That(System.Text.Encoding.ASCII.GetString(utf8, 0, bytes)).IsEqualTo(codec.Encode(4572721));
	}
}
