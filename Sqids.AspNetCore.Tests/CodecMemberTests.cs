using System.Reflection;
using System.Text;

namespace Sqids.AspNetCore.Tests;

public sealed class CodecMemberTests
{
	private static readonly MethodInfo DescribeMethod =
		typeof(SqidCodec).GetMethod("Describe", BindingFlags.NonPublic | BindingFlags.Static)!;

	private static string Describe(SqidParseError error) => (string)DescribeMethod.Invoke(null, [error])!;

	[Test]
	[Arguments(SqidParseError.Empty, "The ID is empty.")]
	[Arguments(SqidParseError.InvalidLength, "The ID has an invalid length.")]
	[Arguments(SqidParseError.InvalidCharacter, "The ID contains characters that are not part of the alphabet.")]
	[Arguments(SqidParseError.Malformed, "The ID is malformed.")]
	[Arguments(SqidParseError.Overflow, "The ID encodes a number that is too large.")]
	[Arguments(SqidParseError.MultipleValues, "The ID encodes more than one number.")]
	[Arguments(SqidParseError.NotCanonical, "The ID is not in canonical form.")]
	public async Task Describe_UsesSpecMessage(SqidParseError error, string expected)
		=> await Assert.That(Describe(error)).IsEqualTo(expected);

	[Test]
	public async Task Describe_FallsBackForNoneAndUnknown()
	{
		await Assert.That(Describe(SqidParseError.None)).IsEqualTo("The ID is invalid.");
		await Assert.That(Describe((SqidParseError)200)).IsEqualTo("The ID is invalid.");
	}

	[Test]
	public async Task TryDecode_Utf8_CoversEmptyLengthNonAsciiAndSuccess()
	{
		var codec = new SqidCodec();

		await Assert.That(codec.TryDecode(ReadOnlySpan<byte>.Empty, out _)).IsEqualTo(SqidParseError.Empty);
		await Assert.That(codec.TryDecode(new byte[256], out _)).IsEqualTo(SqidParseError.InvalidLength);
		await Assert.That(codec.TryDecode(new byte[] { 0xC3, 0xA4 }, out _)).IsEqualTo(SqidParseError.InvalidCharacter);

		var canonical = Encoding.UTF8.GetBytes(codec.Encode(1));
		await Assert.That(codec.TryDecode(canonical, out var value)).IsEqualTo(SqidParseError.None);
		await Assert.That(value).IsEqualTo(1L);
	}
}
