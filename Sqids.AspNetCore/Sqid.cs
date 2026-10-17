using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Text.Json.Serialization;

namespace Sqids.AspNetCore;

/// <summary>
/// A non-negative 64-bit identifier that is written as a Sqids string (e.g. <c>"Uk"</c>) everywhere it is formatted,
/// parsed, bound, serialized to JSON, or described in OpenAPI.
/// </summary>
/// <remarks>
/// Parsing is strict: only the canonical, single-number ID of the value is accepted. The text form is produced by the
/// process-wide <see cref="Codec"/>; call <see cref="Configure"/> once at startup to change alphabet, minimum length or blocklist.
/// </remarks>
[DebuggerDisplay("{ToString(),nq} ({Value})")]
[JsonConverter(typeof(SqidJsonConverter))]
[TypeConverter(typeof(SqidTypeConverter))]
public readonly struct Sqid :
	IEquatable<Sqid>,
	IComparable<Sqid>,
	IComparable,
	ISpanFormattable,
	IUtf8SpanFormattable,
	ISpanParsable<Sqid>,
	IUtf8SpanParsable<Sqid>,
	IComparisonOperators<Sqid, Sqid, bool>
{

	/// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
	public Sqid(long value)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(value);
		Value = value;
	}

	/// <summary>The numeric value. <c>default(Sqid)</c> is the (valid) ID of <c>0</c>.</summary>
	public long Value { get; }

	public static Sqid From(long value) => new(value);

	public static bool TryCreate(long value, out Sqid sqid)
	{
		sqid = value >= 0 ? new Sqid(value) : default;
		return value >= 0;
	}

	public static explicit operator Sqid(long value) => new(value);

	public static explicit operator long(Sqid sqid) => sqid.Value;

	// ----------------------------------------------------------------------------------------------------
	// Process-wide codec
	// ----------------------------------------------------------------------------------------------------

	private static SqidCodec? _codec;

	/// <summary>The codec used for all text conversions. Created with default options on first use unless <see cref="Configure"/> ran first.</summary>
	public static SqidCodec Codec => Volatile.Read(ref _codec) ?? CreateDefaultCodec();

	/// <summary>Sets the process-wide codec. Call once, before any <see cref="Sqid"/> is formatted or parsed.</summary>
	/// <exception cref="InvalidOperationException">A codec is already in use.</exception>
	public static void Configure(SqidOptions options)
	{
		var codec = new SqidCodec(options);
		if (Interlocked.CompareExchange(ref _codec, codec, null) is not null)
		{
			throw new InvalidOperationException(
				"Sqid is already in use with a codec. Call Sqid.Configure (or AddSqids) once, before the first Sqid is formatted or parsed.");
		}
	}

	private static SqidCodec CreateDefaultCodec()
	{
		var created = new SqidCodec();
		return Interlocked.CompareExchange(ref _codec, created, null) ?? created;
	}

	// ----------------------------------------------------------------------------------------------------
	// Formatting
	// ----------------------------------------------------------------------------------------------------

	public override string ToString() => Codec.Encode(Value);

	string IFormattable.ToString(string? format, IFormatProvider? formatProvider)
	{
		ValidateFormat(format);
		return Codec.Encode(Value);
	}

	public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
	{
		ValidateFormat(format);
		return Codec.TryEncode(Value, destination, out charsWritten);
	}

	public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
	{
		ValidateFormat(format);
		return Codec.TryEncode(Value, utf8Destination, out bytesWritten);
	}

	private static void ValidateFormat(ReadOnlySpan<char> format)
	{
		// Only the general format ("G", or none) exists.
		if (!format.IsEmpty && !format.Equals("G", StringComparison.OrdinalIgnoreCase))
		{
			throw new FormatException($"Unknown Sqid format string '{format}'.");
		}
	}

	// ----------------------------------------------------------------------------------------------------
	// Parsing
	// ----------------------------------------------------------------------------------------------------

	/// <summary>Strict parse with a precise failure reason. Never throws.</summary>
	public static bool TryParse(ReadOnlySpan<char> s, out Sqid result, out SqidParseError error)
	{
		error = Codec.TryDecode(s, out var value);
		result = error == SqidParseError.None ? new Sqid(value) : default;
		return error == SqidParseError.None;
	}

	/// <summary>Strict parse of UTF-8 text with a precise failure reason. Never throws.</summary>
	public static bool TryParse(ReadOnlySpan<byte> utf8Text, out Sqid result, out SqidParseError error)
	{
		error = Codec.TryDecode(utf8Text, out var value);
		result = error == SqidParseError.None ? new Sqid(value) : default;
		return error == SqidParseError.None;
	}

	public static bool TryParse(ReadOnlySpan<char> s, out Sqid result) => TryParse(s, out result, out _);

	public static bool TryParse([NotNullWhen(true)] string? s, out Sqid result) => TryParse(s, CultureInfo.InvariantCulture, out result);

	// Signatures below are what IParsable<T>/ISpanParsable<T> require, and what Minimal APIs look for when binding.
	public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Sqid result)
	{
		if (s is not null)
		{
			return TryParse(s.AsSpan(), out result, out _);
		}

		result = default;
		return false;

	}

	public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Sqid result) => TryParse(s, out result, out _);

	public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Sqid result) => TryParse(utf8Text, out result, out _);

	/// <exception cref="FormatException">The text is not the canonical Sqid of a single number.</exception>
	public static Sqid Parse(string s, IFormatProvider? provider = null)
	{
		ArgumentNullException.ThrowIfNull(s);
		return Parse(s.AsSpan(), provider);
	}

	/// <exception cref="FormatException">The text is not the canonical Sqid of a single number.</exception>
	public static Sqid Parse(ReadOnlySpan<char> s, IFormatProvider? provider = null)
		=> TryParse(s, out var result, out var error)
			? result
			: throw new FormatException(SqidCodec.Describe(error));

	/// <exception cref="FormatException">The text is not the canonical Sqid of a single number.</exception>
	public static Sqid Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider = null)
		=> TryParse(utf8Text, out var result, out var error)
			? result
			: throw new FormatException(SqidCodec.Describe(error));

	// ----------------------------------------------------------------------------------------------------
	// Equality / ordering (numeric, not lexical)
	// ----------------------------------------------------------------------------------------------------

	public bool Equals(Sqid other) => Value == other.Value;

	public override bool Equals([NotNullWhen(true)] object? obj) => obj is Sqid other && Equals(other);

	public override int GetHashCode() => Value.GetHashCode();

	public int CompareTo(Sqid other) => Value.CompareTo(other.Value);

	public int CompareTo(object? obj) => obj switch
	{
		null => 1,
		Sqid other => CompareTo(other),
		_ => throw new ArgumentException($"Object must be of type {nameof(Sqid)}.", nameof(obj)),
	};

	public static bool operator ==(Sqid left, Sqid right) => left.Value == right.Value;

	public static bool operator !=(Sqid left, Sqid right) => left.Value != right.Value;

	public static bool operator <(Sqid left, Sqid right) => left.Value < right.Value;

	public static bool operator >(Sqid left, Sqid right) => left.Value > right.Value;

	public static bool operator <=(Sqid left, Sqid right) => left.Value <= right.Value;

	public static bool operator >=(Sqid left, Sqid right) => left.Value >= right.Value;
}