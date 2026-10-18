using BenchmarkDotNet.Attributes;
using Sqids;
using Sqids.AspNetCore;
using OurSqidOptions = Sqids.AspNetCore.SqidOptions;
using TheirSqidOptions = Sqids.SqidsOptions;

namespace Sqids.AspNetCore.Benchmarks;

/// <summary>
/// Encoding a single number — the exact scenario <see cref="SqidCodec"/> is specialised for.
/// <c>SqidsEncoder&lt;long&gt;</c> is the official Sqids library's API for the same job.
/// </summary>
[MemoryDiagnoser]
[RankColumn]
public class EncodeBenchmarks
{
	private SqidCodec _codec = null!;
	private SqidsEncoder<long> _official = null!;

	/// <summary>Short ("Uk"-class), typical, 33-bit, and maximum values.</summary>
	[Params(1L, 123_456L, 4_294_967_296L, long.MaxValue)]
	public long Value { get; set; }

	/// <summary>0 = default (no padding); 8 exercises the MinLength padding path on both sides.</summary>
	[Params(0, 8)]
	public int MinLength { get; set; }

	[GlobalSetup]
	public void Setup()
	{
		_codec = new SqidCodec(new OurSqidOptions { MinLength = MinLength });
		_official = new SqidsEncoder<long>(new TheirSqidOptions { MinLength = MinLength });

		Parity.EnsureIdsMatch(_official.Encode(Value), _codec.Encode(Value));
	}

	[Benchmark(Baseline = true)]
	public string SqidsNet() => _official.Encode(Value);

	[Benchmark]
	public string SqidCodec() => _codec.Encode(Value);
}
