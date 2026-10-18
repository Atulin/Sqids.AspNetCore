namespace Sqids.AspNetCore.Benchmarks;

/// <summary>
/// Benchmark hygiene: both implementations follow the same spec and use the same default options,
/// so every setup verifies they agree byte-for-byte before measuring anything.
/// </summary>
internal static class Parity
{
	public static void EnsureIdsMatch(string officialId, string ourId)
	{
		if (!string.Equals(officialId, ourId, StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				$"Implementations disagree: Sqids.NET encoded '{officialId}', SqidCodec encoded '{ourId}'.");
		}
	}

	public static void EnsureValue(long expected, long actual, string who)
	{
		if (actual != expected)
		{
			throw new InvalidOperationException($"{who} decoded '{actual}' instead of '{expected}'.");
		}
	}
}
