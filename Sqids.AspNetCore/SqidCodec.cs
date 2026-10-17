using System.Buffers;
using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using System.Text;

[module: SkipLocalsInit]

namespace Sqids.AspNetCore;

/// <summary>Why a textual <see cref="Sqid"/> was rejected.</summary>
public enum SqidParseError : byte
{
	None = 0,
	Empty,
	InvalidLength,
	InvalidCharacter,
	Malformed,
	Overflow,

	/// <summary>The ID is a valid Sqids ID, but it encodes more than one number.</summary>
	MultipleValues,

	/// <summary>The ID decodes, but re-encoding the value does not give back the exact same text.</summary>
	NotCanonical,
}

/// <summary>Configuration for <see cref="SqidCodec"/>. Mirrors the options of the official Sqids libraries.</summary>
public sealed class SqidOptions
{
	/// <summary>
	/// Alphabet used to build IDs. Must be 3-94 unique, visible ASCII characters.
	/// Supply your own shuffled alphabet so IDs differ from every other Sqids user's.
	/// </summary>
	public string Alphabet { get; set; } = SqidCodec.DefaultAlphabet;

	/// <summary>Minimum ID length (0-255).</summary>
	public int MinLength { get; set; }

	/// <summary>
	/// Words IDs must not contain. <see langword="null"/> (default) uses the official Sqids blocklist;
	/// an empty collection disables blocklisting.
	/// </summary>
	public IEnumerable<string>? Blocklist { get; set; }
}

/// <summary>
/// Immutable, thread-safe Sqids encoder/decoder specialised for exactly one non-negative <see cref="long"/> per ID.
/// Output is byte-for-byte identical to the official Sqids spec for single-number IDs.
/// </summary>
public sealed class SqidCodec
{
	public const string DefaultAlphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

	// Spec caps MinLength at 255 (u8), so no ID is ever longer than this.
	private const int BufferSize = 255;
	private const int MaxAlphabetLength = 94; // visible ASCII: '!'..'~'

	private readonly char[] _alphabet; // shuffled once, as the spec does
	private readonly sbyte[] _lookup; // ASCII char -> index in _alphabet, -1 if absent
	private readonly SearchValues<char> _alphabetChars;
	private readonly int _n;
	private readonly ulong _fastModN; // Lemire's fastmod constant for _n, see Shuffle
	private readonly ulong _overflowQuotient; // long.MaxValue / base
	private readonly ulong _overflowRemainder; // long.MaxValue % base

	// Blocklist, split by the spec's matching rules:
	//   3-letter words  -> must equal the whole ID
	//   words with digits (leetspeak) -> match only at the start or end of the ID
	//   everything else -> match anywhere
	private readonly bool _hasExact3;
	private readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> _exact3;
	private readonly bool _hasLeet;
	private readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> _leet;
	private readonly int[] _leetLengths = [];
	private readonly SearchValues<string>? _contains;

	// Cheap pre-filter for the two groups above: a bit set holding the (case-folded) first trigram of every
	// blocklist word of 4+ chars. An ID can only contain such a word if one of its own trigrams is in the set,
	// so the vast majority of IDs skip the multi-string search entirely.
	private readonly ulong[]? _trigramFilter;

	// minLength padding depends only on the offset, never on the value, so every possible padding is precomputed:
	// row `offset` = separator followed by the junk stream, MinLength - 2 chars (the longest padding ever needed).
	private readonly char[]? _padding;
	private const int FilterBits = 17;

	public SqidCodec() : this(new SqidOptions())
	{}

	public SqidCodec(SqidOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var alphabet = options.Alphabet ?? throw new ArgumentException("Alphabet must not be null.", nameof(options));
		var minLength = options.MinLength;

		if (alphabet.Length is < 3 or > MaxAlphabetLength)
		{
			throw new ArgumentException($"Alphabet length must be between 3 and {MaxAlphabetLength}.", nameof(options));
		}

		Span<bool> seen = stackalloc bool[128];
		seen.Clear();
		foreach (var c in alphabet)
		{
			if (c is <= ' ' or > '~')
			{
				throw new ArgumentException("Alphabet may only contain visible ASCII characters.", nameof(options));
			}

			if (seen[c])
			{
				throw new ArgumentException("Alphabet must contain unique characters.", nameof(options));
			}

			seen[c] = true;
		}

		if ((uint)minLength > BufferSize)
		{
			throw new ArgumentException($"MinLength must be between 0 and {BufferSize}.", nameof(options));
		}

		var shuffled = alphabet.ToCharArray();
		_fastModN = ulong.MaxValue / (ulong)shuffled.Length + 1;
		Shuffle(shuffled);

		_alphabet = shuffled;
		_n = shuffled.Length;
		MinLength = minLength;
		_alphabetChars = SearchValues.Create(shuffled);

		_lookup = new sbyte[128];
		Array.Fill(_lookup, (sbyte)-1);
		for (var i = 0; i < shuffled.Length; i++)
		{
			_lookup[shuffled[i]] = (sbyte)i;
		}

		if (minLength > 2)
		{
			var rowLength = minLength - 2;
			_padding = new char[_n * rowLength];
			Span<char> scratch = stackalloc char[MaxAlphabetLength];
			scratch = scratch[.._n];
			for (var offset = 0; offset < _n; offset++)
			{
				FillReversedRotated(scratch, offset);
				var row = _padding.AsSpan(offset * rowLength, rowLength);
				row[0] = scratch[0];
				for (var filled = 1; filled < rowLength;)
				{
					Shuffle(scratch);
					var take = Math.Min(rowLength - filled, _n);
					scratch[..take].CopyTo(row[filled..]);
					filled += take;
				}
			}
		}

		var b = (ulong)(_n - 1);
		_overflowQuotient = long.MaxValue / b;
		_overflowRemainder = long.MaxValue % b;

		var digits = 0;
		for (var t = (ulong)long.MaxValue; t != 0; t /= b)
		{
			digits++;
		}

		MinIdLength = Math.Max(2, minLength); // prefix + one digit
		MaxIdLength = Math.Max(1 + digits, minLength); // prefix + digits of long.MaxValue

		AlphabetCharacters = new string(shuffled.Order().ToArray());

		// ---- blocklist -------------------------------------------------------------------------------------
		var allowed = new bool[128]; // lower-cased alphabet, as in the spec
		foreach (var c in alphabet)
		{
			allowed[char.ToLowerInvariant(c)] = true;
		}

		var exact3 = new HashSet<string>(StringComparer.Ordinal);
		var leet = new HashSet<string>(StringComparer.Ordinal);
		var contains = new HashSet<string>(StringComparer.Ordinal);

		foreach (var word in options.Blocklist ?? SqidDefaultBlocklist.Words)
		{
			if (word.Length < 3)
			{
				continue;
			}

			var lower = word.ToLowerInvariant();
			var usable = true;
			foreach (var c in lower)
			{
				if (c < 128 && allowed[c])
				{
					continue;
				}

				usable = false;
				break;
			}

			if (!usable)
			{
				continue;
			}

			if (lower.Length == 3)
			{
				exact3.Add(lower);
			}
			else if (lower.AsSpan().ContainsAnyInRange('0', '9'))
			{
				leet.Add(lower);
			}
			else
			{
				contains.Add(lower);
			}
		}

		if (exact3.Count > 0)
		{
			_exact3 = exact3.ToFrozenSet(StringComparer.OrdinalIgnoreCase).GetAlternateLookup<ReadOnlySpan<char>>();
			_hasExact3 = true;
		}

		if (leet.Count > 0)
		{
			_leet = leet.ToFrozenSet(StringComparer.OrdinalIgnoreCase).GetAlternateLookup<ReadOnlySpan<char>>();
			_leetLengths = leet.Select(static w => w.Length).Distinct().Order().ToArray();
			_hasLeet = true;
		}

		if (contains.Count > 0)
		{
			_contains = SearchValues.Create(contains.ToArray(), StringComparison.OrdinalIgnoreCase);
		}

		if (leet.Count <= 0 && contains.Count <= 0)
		{
			return;
		}
		
		var filter = new ulong[(1 << FilterBits) / 64];
		foreach (var word in leet.Concat(contains))
		{
			var bit = TrigramBit(word[0], word[1], word[2]);
			filter[bit >> 6] |= 1UL << (bit & 63);
		}

		_trigramFilter = filter;

	}

	/// <summary>The configured minimum ID length.</summary>
	public int MinLength { get; }

	/// <summary>Shortest ID this codec can produce.</summary>
	public int MinIdLength { get; }

	/// <summary>Longest ID this codec can produce (the ID for <see cref="long.MaxValue"/>, or <see cref="MinLength"/>).</summary>
	public int MaxIdLength { get; }

	/// <summary>The alphabet's characters in sorted order (not the secret shuffled order).</summary>
	public string AlphabetCharacters { get; }

	// ======================================================================================================
	// Encoding
	// ======================================================================================================

	/// <summary>Encodes <paramref name="value"/>. Allocates exactly one string.</summary>
	public string Encode(long value)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(value);

		Span<char> buffer = stackalloc char[BufferSize];
		var written = EncodeCore((ulong)value, buffer);
		return new string(buffer[..written]);
	}

	/// <summary>Encodes into UTF-16. Returns <see langword="false"/> if the value is negative or the destination is too small.</summary>
	public bool TryEncode(long value, Span<char> destination, out int charsWritten)
	{
		if (value < 0)
		{
			charsWritten = 0;
			return false;
		}

		Span<char> buffer = stackalloc char[BufferSize];
		var written = EncodeCore((ulong)value, buffer);
		if (written > destination.Length)
		{
			charsWritten = 0;
			return false;
		}

		buffer[..written].CopyTo(destination);
		charsWritten = written;
		return true;
	}

	/// <summary>Encodes into UTF-8 (IDs are pure ASCII). Returns <see langword="false"/> if the value is negative or the destination is too small.</summary>
	public bool TryEncode(long value, Span<byte> utf8Destination, out int bytesWritten)
	{
		if (value < 0)
		{
			bytesWritten = 0;
			return false;
		}

		Span<char> buffer = stackalloc char[BufferSize];
		var written = EncodeCore((ulong)value, buffer);
		if (Ascii.FromUtf16(buffer[..written], utf8Destination, out bytesWritten) == OperationStatus.Done)
		{
			return true;
		}
		
		bytesWritten = 0;
		return false;

	}

	/// <param name="value">Value to encode.</param>
	/// <param name="destination">Must be at least <see cref="BufferSize"/> long.</param>
	private int EncodeCore(ulong value, Span<char> destination)
	{
		// Spec: offset = (numbers.Length + alphabet[v % n] + index) % n, with one number at index 0.
		var baseOffset = (int)((1u + _alphabet[value <= uint.MaxValue ? (int)((uint)value % (uint)_n) : (int)(value % (ulong)_n)]) % (uint)_n);

		// Spec: if the ID contains a blocked word, retry with offset + 1, up to alphabet.Length times.
		for (var increment = 0; increment <= _n; increment++)
		{
			var offset = baseOffset + increment; // < 2n
			if (offset >= _n)
			{
				offset -= _n;
			}

			var length = WriteId(value, offset, destination);
			if (!IsBlocked(destination[..length]))
			{
				return length;
			}
		}

		throw new InvalidOperationException("Reached max attempts to re-generate the ID.");
	}

	private int WriteId(ulong value, int offset, Span<char> destination)
	{
		var alpha = _alphabet;

		// The spec rotates the alphabet by `offset`, takes its first char as the prefix, reverses it,
		// reserves the first char of the reversed alphabet as separator and encodes digits with the rest.
		// With a single number we never need the materialised alphabet, only index arithmetic:
		//   prefix       = alpha[offset]
		//   digit d      = alpha[(offset + n - 2 - d) % n]      (d in 0..n-2)
		//   separator    = alpha[(offset + n - 1) % n]
		destination[0] = alpha[offset];

		var b = (ulong)(_n - 1);
		var position = 1;

		// 64-bit division is several times slower than 32-bit; only values above 2^32 pay for it, and only for their top digits.
		while (value > uint.MaxValue)
		{
			var quotient = value / b;
			var digit = (int)(value - quotient * b);
			var index = offset + _n - 2 - digit;
			if (index >= _n)
			{
				index -= _n;
			}

			destination[position++] = alpha[index];
			value = quotient;
		}

		var value32 = (uint)value;
		var b32 = (uint)b;
		do
		{
			var quotient = value32 / b32;
			var digit = (int)(value32 - quotient * b32);
			var index = offset + _n - 2 - digit;
			if (index >= _n)
			{
				index -= _n;
			}

			destination[position++] = alpha[index];
			value32 = quotient;
		} while (value32 != 0);

		destination[1..position].Reverse(); // digits were produced least-significant first

		if (position >= MinLength)
		{
			return position;
		}

		// minLength padding: separator, then junk from the repeatedly shuffled alphabet (precomputed per offset).
		// Decoders recognise the padding because the char after the separator is the *shuffled* separator.
		var padLength = MinLength - position;
		_padding.AsSpan(offset * (MinLength - 2), padLength).CopyTo(destination[position..]);
		return MinLength;
	}

	private void FillReversedRotated(Span<char> destination, int offset)
	{
		var alpha = _alphabet;
		for (var k = 0; k < _n; k++)
		{
			var index = offset + _n - 1 - k;
			if (index >= _n)
			{
				index -= _n;
			}

			destination[k] = alpha[index];
		}
	}

	// ======================================================================================================
	// Decoding
	// ======================================================================================================

	/// <summary>
	/// Decodes a UTF-16 ID. Succeeds only for the single, canonical ID of one number: the value is re-encoded and the
	/// result must match the input exactly (which also rejects multi-number IDs, leading-zero digits, wrong padding,
	/// IDs that would have been blocked, and IDs produced with a different alphabet or minimum length).
	/// </summary>
	public SqidParseError TryDecode(ReadOnlySpan<char> id, out long value) => DecodeCore(id, out value);

	/// <summary>UTF-8 overload of <see cref="TryDecode(ReadOnlySpan{char}, out long)"/>.</summary>
	public SqidParseError TryDecode(ReadOnlySpan<byte> utf8Id, out long value)
	{
		value = 0;
		if (utf8Id.IsEmpty)
		{
			return SqidParseError.Empty;
		}

		if (utf8Id.Length > BufferSize)
		{
			return SqidParseError.InvalidLength;
		}

		Span<char> buffer = stackalloc char[BufferSize];
		if (Ascii.ToUtf16(utf8Id, buffer, out var written) != OperationStatus.Done)
		{
			return SqidParseError.InvalidCharacter;
		}

		return DecodeCore(buffer[..written], out value);
	}

	private SqidParseError DecodeCore(ReadOnlySpan<char> id, out long value)
	{
		value = 0;

		if (id.IsEmpty)
		{
			return SqidParseError.Empty;
		}

		if ((uint)(id.Length - MinIdLength) > (uint)(MaxIdLength - MinIdLength))
		{
			return SqidParseError.InvalidLength;
		}

		if (id.ContainsAnyExcept(_alphabetChars))
		{
			return SqidParseError.InvalidCharacter;
		}

		var lookup = _lookup;

		// Spec: the first char is the prefix; its position in the alphabet is the (incremented) offset.
		int offset = lookup[id[0]];

		// Spec: rotate by offset, reverse; the first char is the separator, the rest are digits (most significant first).
		// For a char at alphabet index a: r = (a - offset) mod n; r == n - 1 -> separator, else digit = n - 2 - r.
		ulong accumulator = 0;
		var i = 1;
		for (; i < id.Length; i++)
		{
			var r = lookup[id[i]] - offset;
			if (r < 0)
			{
				r += _n;
			}

			if (r == _n - 1)
			{
				break; // separator
			}

			var digit = (ulong)(_n - 2 - r);
			if (accumulator > _overflowQuotient || (accumulator == _overflowQuotient && digit > _overflowRemainder))
			{
				return SqidParseError.Overflow;
			}

			accumulator = accumulator * (ulong)(_n - 1) + digit;
		}

		if (i == 1)
		{
			return SqidParseError.Malformed; // no digits before the separator
		}

		// A separator was found. Either the rest is minLength padding (the spec's decoder shuffles once and then
		// sees its new separator immediately), or another number follows.
		if (i < id.Length - 1 && id[i + 1] != ShuffledSeparator(offset))
		{
			return SqidParseError.MultipleValues;
		}

		// Canonical check: the one and only valid text for this value is what we would have produced.
		Span<char> expected = stackalloc char[BufferSize];
		var written = EncodeCore(accumulator, expected);
		if (!expected[..written].SequenceEqual(id))
		{
			return SqidParseError.NotCanonical;
		}

		value = (long)accumulator;
		return SqidParseError.None;
	}

	private char ShuffledSeparator(int offset)
	{
		// Padded IDs: it is the first junk char, already precomputed.
		if (_padding is not null && MinLength >= 4)
		{
			return _padding[offset * (MinLength - 2) + 1];
		}

		Span<char> scratch = stackalloc char[MaxAlphabetLength];
		scratch = scratch[.._n];
		FillReversedRotated(scratch, offset);
		Shuffle(scratch);
		return scratch[0];
	}

	// ======================================================================================================
	// Shared
	// ======================================================================================================

	// The spec's consistent shuffle: r = (i * j + chars[i] + chars[j]) % n, then swap(i, r).
	// The modulo runs n/2 times per shuffle, so it uses Lemire's multiply-only fastmod (exact for 32-bit operands).
	private void Shuffle(Span<char> chars)
	{
		var n = chars.Length;
		for (int i = 0, j = n - 1; j > 0; i++, j--)
		{
			var a = (uint)(i * j + chars[i] + chars[j]);
			var r = (int)Math.BigMul(_fastModN * a, (ulong)n, out _);
			(chars[i], chars[r]) = (chars[r], chars[i]);
		}
	}

	private bool IsBlocked(ReadOnlySpan<char> id)
	{
		// Words are >= 3 chars, so a shorter ID can never be blocked, and 3-char words must match the whole ID.
		if (id.Length < 3)
		{
			return false;
		}

		if (id.Length == 3)
		{
			return _hasExact3 && _exact3.Contains(id);
		}

		if (_trigramFilter is null || !MayContainBlockedWord(id))
		{
			return false;
		}

		if (_hasLeet)
		{
			foreach (var length in _leetLengths)
			{
				if (length > id.Length)
				{
					break; // lengths are sorted
				}

				if (_leet.Contains(id[..length]) || _leet.Contains(id[^length..]))
				{
					return true;
				}
			}
		}

		return _contains is not null && id.ContainsAny(_contains);
	}

	private bool MayContainBlockedWord(ReadOnlySpan<char> id)
	{
		var filter = _trigramFilter!;
		for (var i = 0; i + 2 < id.Length; i++)
		{
			var bit = TrigramBit(id[i], id[i + 1], id[i + 2]);
			if ((filter[bit >> 6] & (1UL << (bit & 63))) != 0)
			{
				return true;
			}
		}

		return false;
	}

	private static int TrigramBit(char a, char b, char c)
	{
		var key = ((uint)Fold(a) << 14) | ((uint)Fold(b) << 7) | (uint)Fold(c); // 7 bits each: IDs are ASCII
		return (int)((key * 2654435761u) >> (32 - FilterBits));
	}

	private static int Fold(char c) => (uint)(c - 'A') <= 'Z' - 'A' ? c + ('a' - 'A') : c;

	internal static string Describe(SqidParseError error) => error switch
	{
		SqidParseError.Empty => "The ID is empty.",
		SqidParseError.InvalidLength => "The ID has an invalid length.",
		SqidParseError.InvalidCharacter => "The ID contains characters that are not part of the alphabet.",
		SqidParseError.Malformed => "The ID is malformed.",
		SqidParseError.Overflow => "The ID encodes a number that is too large.",
		SqidParseError.MultipleValues => "The ID encodes more than one number.",
		SqidParseError.NotCanonical => "The ID is not in canonical form.",
		_ => "The ID is invalid.",
	};
}