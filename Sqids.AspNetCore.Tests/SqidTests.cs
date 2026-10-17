using System.Globalization;

namespace Sqids.AspNetCore.Tests;

public sealed class SqidTests
{
	[Test]
	public async Task Constructor_Negative_Throws()
	{
		await Assert.That(() => new Sqid(-1)).Throws<ArgumentOutOfRangeException>();
	}

	[Test]
	public async Task Default_IsTheIdOfZero()
	{
		await Assert.That(default(Sqid).Value).IsEqualTo(0L);
	}

	[Test]
	public async Task FromAndExplicitOperators_RoundTrip()
	{
		await Assert.That(Sqid.From(5).Value).IsEqualTo(5L);
		await Assert.That((Sqid)5L).IsEqualTo(Sqid.From(5));
		await Assert.That((long)Sqid.From(5)).IsEqualTo(5L);
	}

	[Test]
	public async Task TryCreate_RejectsNegativeValues()
	{
		await Assert.That(Sqid.TryCreate(-3, out var rejected)).IsFalse();
		await Assert.That(rejected).IsEqualTo(default(Sqid));

		await Assert.That(Sqid.TryCreate(3, out var created)).IsTrue();
		await Assert.That(created.Value).IsEqualTo(3L);
	}

	[Test]
	public async Task ToString_UsesTheCanonicalText()
	{
		await Assert.That(Sqid.From(1).ToString()).IsEqualTo("Uk");
	}

	[Test]
	public async Task IFormattable_AcceptsOnlyTheGeneralFormat()
	{
		var sqid = Sqid.From(1);
		await Assert.That(((IFormattable)sqid).ToString("G", null)).IsEqualTo("Uk");
		await Assert.That(((IFormattable)sqid).ToString("g", null)).IsEqualTo("Uk");
		await Assert.That(((IFormattable)sqid).ToString(null, null)).IsEqualTo("Uk");
		await Assert.That(((IFormattable)sqid).ToString("", null)).IsEqualTo("Uk");
	}

	[Test]
	public async Task IFormattable_UnknownFormat_Throws()
	{
		var sqid = Sqid.From(1);
		await Assert.That(() => ((IFormattable)sqid).ToString("X", null)).Throws<FormatException>();
	}

	[Test]
	public async Task TryFormat_Char_WritesAndFailsWhenTooSmall()
	{
		var sqid = Sqid.From(1);
		var buffer = new char[8];

		await Assert.That(sqid.TryFormat(buffer, out var written)).IsTrue();
		await Assert.That(new string(buffer, 0, written)).IsEqualTo("Uk");
		await Assert.That(sqid.TryFormat(buffer.AsSpan(0, 1), out _)).IsFalse();
	}

	[Test]
	public async Task TryFormat_Utf8_Writes()
	{
		var sqid = Sqid.From(1);
		var buffer = new byte[8];

		await Assert.That(sqid.TryFormat(buffer, out var written)).IsTrue();
		await Assert.That(System.Text.Encoding.UTF8.GetString(buffer, 0, written)).IsEqualTo("Uk");
	}

	[Test]
	public async Task TryFormat_UnknownFormat_Throws()
	{
		var sqid = Sqid.From(1);
		await Assert.That(() => sqid.TryFormat(new char[8], out _, "X")).Throws<FormatException>();
		await Assert.That(() => sqid.TryFormat(new byte[8], out _, "X")).Throws<FormatException>();
	}

	[Test]
	public async Task Parse_RoundTrips()
	{
		await Assert.That(Sqid.Parse("Uk")).IsEqualTo(Sqid.From(1));
	}

	[Test]
	public async Task Parse_Null_Throws()
	{
		await Assert.That(() => Sqid.Parse((string)null!)).Throws<ArgumentNullException>();
	}

	[Test]
	public async Task Parse_Invalid_ThrowsWithDescribeMessage()
	{
		var ex = TestHelpers.Capture(() => { _ = Sqid.Parse("**"); });
		await Assert.That(ex).IsTypeOf<FormatException>();
		await Assert.That(ex.Message).IsEqualTo("The ID contains characters that are not part of the alphabet.");
	}

	[Test]
	public async Task TryParse_StringOverloads()
	{
		await Assert.That(Sqid.TryParse("Uk", out var value)).IsTrue();
		await Assert.That(value).IsEqualTo(Sqid.From(1));

		await Assert.That(Sqid.TryParse((string?)null, out _)).IsFalse();

		await Assert.That(Sqid.TryParse("Uk", null, out var provided)).IsTrue();
		await Assert.That(provided).IsEqualTo(Sqid.From(1));
	}

	[Test]
	public async Task TryParse_SpanOverloads_ReportTheError()
	{
		await Assert.That(Sqid.TryParse("Uk".AsSpan(), out var value, out var error)).IsTrue();
		await Assert.That(error).IsEqualTo(SqidParseError.None);
		await Assert.That(value).IsEqualTo(Sqid.From(1));

		await Assert.That(Sqid.TryParse("**".AsSpan(), out _, out var invalid)).IsFalse();
		await Assert.That(invalid).IsEqualTo(SqidParseError.InvalidCharacter);

		await Assert.That(Sqid.TryParse("**".AsSpan(), null, out _)).IsFalse();
	}

	[Test]
	public async Task TryParse_SpanWithoutProvider_ReportsTheError()
	{
		await Assert.That(Sqid.TryParse("Uk".AsSpan(), out var value)).IsTrue();
		await Assert.That(value).IsEqualTo(Sqid.From(1));

		await Assert.That(Sqid.TryParse("**".AsSpan(), out var rejected)).IsFalse();
		await Assert.That(rejected).IsEqualTo(default(Sqid));
	}

	[Test]
	public async Task TryParse_Utf8_ReportTheError()
	{
		await Assert.That(Sqid.TryParse("Uk"u8, out var value, out var error)).IsTrue();
		await Assert.That(error).IsEqualTo(SqidParseError.None);
		await Assert.That(value).IsEqualTo(Sqid.From(1));

		await Assert.That(Sqid.TryParse("**"u8, out _, out var invalid)).IsFalse();
		await Assert.That(invalid).IsEqualTo(SqidParseError.InvalidCharacter);

		await Assert.That(Sqid.TryParse("Uk"u8, null, out var provided)).IsTrue();
		await Assert.That(provided).IsEqualTo(Sqid.From(1));
	}

	[Test]
	public async Task Parse_SpanAndUtf8_RoundTrip()
	{
		await Assert.That(Sqid.Parse("Uk".AsSpan(), null)).IsEqualTo(Sqid.From(1));
		await Assert.That(Sqid.Parse("Uk"u8, null)).IsEqualTo(Sqid.From(1));

		var spanEx = TestHelpers.Capture(() => { _ = Sqid.Parse("**".AsSpan(), null); });
		await Assert.That(spanEx).IsTypeOf<FormatException>();

		var utf8Ex = TestHelpers.Capture(() => { _ = Sqid.Parse("**"u8, null); });
		await Assert.That(utf8Ex).IsTypeOf<FormatException>();
	}

	[Test]
	public async Task Parse_ToString_RoundTripsAcrossTheRange()
	{
		foreach (var value in new[] { 0L, 1L, 100L, 1_000_000L, long.MaxValue })
		{
			var text = Sqid.From(value).ToString();
			await Assert.That(Sqid.Parse(text)).IsEqualTo(Sqid.From(value));
		}
	}

	[Test]
	public async Task Equality_IsNumeric()
	{
		var one = Sqid.From(1);
		var two = Sqid.From(2);

		await Assert.That(one == Sqid.From(1)).IsTrue();
		await Assert.That(one != two).IsTrue();
		await Assert.That(one.Equals(Sqid.From(1))).IsTrue();
		await Assert.That(one.Equals((object)two)).IsFalse();
		await Assert.That(one.Equals("nope")).IsFalse();
		await Assert.That(one.GetHashCode()).IsEqualTo(Sqid.From(1).GetHashCode());
	}

	[Test]
	public async Task Ordering_IsNumeric()
	{
		var one = Sqid.From(1);
		var two = Sqid.From(2);

		await Assert.That(one < two).IsTrue();
		await Assert.That(two > one).IsTrue();
		await Assert.That(one <= Sqid.From(1)).IsTrue();
		await Assert.That(one >= Sqid.From(1)).IsTrue();
		await Assert.That(one.CompareTo(Sqid.From(2))).IsLessThan(0);
		await Assert.That(one.CompareTo((object?)null)).IsEqualTo(1);
		await Assert.That(one.CompareTo((object)Sqid.From(1))).IsEqualTo(0);
	}

	[Test]
	public async Task CompareTo_WrongType_Throws()
	{
		var one = Sqid.From(1);
		await Assert.That(() => one.CompareTo("nope")).Throws<ArgumentException>();
	}
}
