using Sqids;

namespace Sqids.AspNetCore.Tests;

public sealed class CodecBlocklistTests
{
	private const string Alpha = "0123ab";

	[Test]
	public async Task Encode_Exact3BlockedWord_RetriesAndAvoidsWholeId()
	{
		var plain = new SqidCodec(new SqidOptions { Blocklist = [] });
		var blocked = new SqidCodec(new SqidOptions { Blocklist = ["86u"] });

		await Assert.That(plain.Encode(100)).IsEqualTo("86u");

		var blockedId = blocked.Encode(100);
		await Assert.That(blockedId).IsNotEqualTo("86u");
		await Assert.That(blocked.TryDecode(blockedId, out var value)).IsEqualTo(SqidParseError.None);
		await Assert.That(value).IsEqualTo(100L);
	}

	// A leet word (one with digits) is matched at the start or the end of an ID, so the test needs a word this
	// alphabet can actually put there. The spec's shuffle of "0123ab" is 1,a,3,0,2,b: ids starting with 'a' are
	// followed by the separator '1', so no id can ever begin with "a1" (which is why "a1b2" used to find nothing).
	// "a00b" has a digit and occurs at both ends: Encode(303) = "a00b3" and Encode(1185) = "a2a00b".
	private const string LeetWord = "a00b";

	[Test]
	public async Task Encode_LeetBlockedWord_AvoidsStartAndEndMatches()
	{
		var plain = new SqidCodec(new SqidOptions { Alphabet = Alpha, Blocklist = [] });
		var blocked = new SqidCodec(new SqidOptions { Alphabet = Alpha, Blocklist = [LeetWord] });

		var startHit = -1L;
		var endHit = -1L;
		for (long v = 0; v < 1_000_000 && (startHit < 0 || endHit < 0); v++)
		{
			var id = plain.Encode(v);
			if (startHit < 0 && id.StartsWith(LeetWord, StringComparison.Ordinal))
			{
				startHit = v;
			}
			else if (endHit < 0 && id.EndsWith(LeetWord, StringComparison.Ordinal))
			{
				endHit = v;
			}
		}

		await Assert.That(startHit).IsGreaterThanOrEqualTo(0L);
		await Assert.That(endHit).IsGreaterThanOrEqualTo(0L);

		foreach (var v in new[] { startHit, endHit })
		{
			var plainId = plain.Encode(v);
			var blockedId = blocked.Encode(v);
			await Assert.That(blockedId).IsNotEqualTo(plainId);
			await Assert.That(
				blockedId.StartsWith(LeetWord, StringComparison.Ordinal) || blockedId.EndsWith(LeetWord, StringComparison.Ordinal))
				.IsFalse();
			await Assert.That(blocked.TryDecode(blockedId, out var value)).IsEqualTo(SqidParseError.None);
			await Assert.That(value).IsEqualTo(v);
		}
	}

	[Test]
	public async Task Encode_BlocklistWordsTheAlphabetCannotSpell_AreIgnored()
	{
		var plain = new SqidCodec(new SqidOptions { Alphabet = Alpha, Blocklist = [] });
		// "ab" is shorter than three characters; "a00x" and "xyzzy" use letters the alphabet does not have.
		// None of them may change a single encoding, unlike the usable leet word "a00b".
		var unusable = new SqidCodec(new SqidOptions { Alphabet = Alpha, Blocklist = ["ab", "a00x", "xyzzy"] });
		var usable = new SqidCodec(new SqidOptions { Alphabet = Alpha, Blocklist = [LeetWord] });

		foreach (var v in new long[] { 0, 1, 42, 303, 1185 })
		{
			await Assert.That(unusable.Encode(v)).IsEqualTo(plain.Encode(v));
		}

		await Assert.That(usable.Encode(303)).IsNotEqualTo(plain.Encode(303));
	}

	[Test]
	public async Task Encode_ContainsBlockedWord_AvoidsAnywhereMatch()
	{
		var plain = new SqidCodec(new SqidOptions { Alphabet = Alpha, Blocklist = [] });
		var blocked = new SqidCodec(new SqidOptions { Alphabet = Alpha, Blocklist = ["abba"] });

		var hit = -1L;
		for (long v = 0; v < 1_000_000 && hit < 0; v++)
		{
			if (plain.Encode(v).Contains("abba", StringComparison.Ordinal))
			{
				hit = v;
			}
		}

		await Assert.That(hit).IsGreaterThanOrEqualTo(0L);

		var plainId = plain.Encode(hit);
		var blockedId = blocked.Encode(hit);
		await Assert.That(plainId).Contains("abba");
		await Assert.That(blockedId.Contains("abba", StringComparison.Ordinal)).IsFalse();
		await Assert.That(blockedId).IsNotEqualTo(plainId);
		await Assert.That(blocked.TryDecode(blockedId, out var value)).IsEqualTo(SqidParseError.None);
		await Assert.That(value).IsEqualTo(hit);
	}

	[Test]
	public async Task Encode_LeetWordsOfSeveralLengths_AvoidsAllAndCoversLengthBreak()
	{
		var blocked = new SqidCodec(new SqidOptions { Alphabet = Alpha, Blocklist = ["a1b2", "1a1b2"] });

		var leaked = false;
		var sawLength4 = false;
		for (long v = 0; v < 100_000; v++)
		{
			var id = blocked.Encode(v);
			sawLength4 |= id.Length == 4;
			if (id.StartsWith("a1b2", StringComparison.Ordinal) || id.EndsWith("a1b2", StringComparison.Ordinal)
				|| id.StartsWith("1a1b2", StringComparison.Ordinal) || id.EndsWith("1a1b2", StringComparison.Ordinal))
			{
				leaked = true;
				break;
			}
		}

		await Assert.That(leaked).IsFalse();
		await Assert.That(sawLength4).IsTrue();
	}

	[Test]
	public async Task Encode_EmptyBlocklist_DisablesEveryGroup()
	{
		var codec = new SqidCodec(new SqidOptions { Blocklist = [] });

		await Assert.That(codec.Encode(100)).IsEqualTo("86u");
	}

	[Test]
	public async Task Encode_DefaultCodec_NeverEmitsABlockedWord()
	{
		var codec = new SqidCodec();
		string[] words =
		[
			"ass", "cum", "goo", "g00", "g0o", "fag",
			"aho1e", "b00b", "sh1t", "f0da",
			"arse", "anus", "fuck", "shit", "dick", "bitch", "pussy", "cunt",
			"cock", "twat", "turd", "rape", "jizz", "dyke", "wank", "poop", "porn", "slut",
		];

		var leaked = false;
		for (long v = 0; v < 100_000 && !leaked; v++)
		{
			var id = codec.Encode(v);
			foreach (var word in words)
			{
				var match = word.Length switch
				{
					3 => id.Length == 3 && id.Equals(word, StringComparison.OrdinalIgnoreCase),
					_ when word.AsSpan().ContainsAnyInRange('0', '9') =>
						id.StartsWith(word, StringComparison.OrdinalIgnoreCase)
						|| id.EndsWith(word, StringComparison.OrdinalIgnoreCase),
					_ => id.Contains(word, StringComparison.OrdinalIgnoreCase),
				};

				if (match)
				{
					leaked = true;
					break;
				}
			}
		}

		await Assert.That(leaked).IsFalse();
	}
}
