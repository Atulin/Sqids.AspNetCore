using BenchmarkDotNet.Attributes;
using Sqids;
using Sqids.AspNetCore;
using OurSqidOptions = Sqids.AspNetCore.SqidOptions;
using TheirSqidOptions = Sqids.SqidsOptions;

namespace Sqids.AspNetCore.Benchmarks;

/// <summary>
/// Decoding a single-number ID — <see cref="SqidCodec.TryDecode"/> against
/// <c>SqidsEncoder&lt;long&gt;.Decode(string)</c>, plus the strict <see cref="Sqid.TryParse"/>
/// public API (canonical-form validation on top of decoding) for context.
/// </summary>
[MemoryDiagnoser]
[RankColumn]
public class DecodeBenchmarks
{
	private SqidCodec _codec = null!;
	private SqidsEncoder<long> _official = null!;
	private string _id = string.Empty;

	[Params(1L, 123_456L, 4_294_967_296L, long.MaxValue)]
	public long Value { get; set; }

	[Params(0, 8)]
	public int MinLength { get; set; }

	[GlobalSetup]
	public void Setup()
	{
		_codec = new SqidCodec(new OurSqidOptions { MinLength = MinLength });
		_official = new SqidsEncoder<long>(new TheirSqidOptions { MinLength = MinLength });

		_id = _official.Encode(Value);
		Parity.EnsureIdsMatch(_id, _codec.Encode(Value));

		var official = _official.Decode(_id);
		if (official is not [var decoded])
		{
			throw new InvalidOperationException($"Sqids.NET decoded '{_id}' into {official.Count} numbers, expected exactly one.");
		}

		Parity.EnsureValue(Value, decoded, "Sqids.NET");

		if (_codec.TryDecode(_id, out var ours) != SqidParseError.None)
		{
			throw new InvalidOperationException($"SqidCodec failed to decode '{_id}'.");
		}

		Parity.EnsureValue(Value, ours, "SqidCodec");
	}

	[Benchmark(Baseline = true)]
	public long SqidsNet() => _official.Decode(_id)[0];

	[Benchmark]
	public long SqidCodec()
	{
		_codec.TryDecode(_id, out var value);
		return value;
	}

	/// <summary>The strict public API: canonical-form validation on top of decoding.</summary>
	[Benchmark]
	public long SqidTryParse()
	{
		Sqid.TryParse(_id, out var value);
		return value.Value;
	}
}
