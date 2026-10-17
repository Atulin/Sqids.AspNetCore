using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Sqids.AspNetCore.Tests;

[NotInParallel]
public sealed class SqidConfigureTests
{
	private static readonly FieldInfo CodecField =
		typeof(Sqid).GetField("_codec", BindingFlags.NonPublic | BindingFlags.Static)!;

	[Test]
	public async Task Configure_SetsTheProcessWideCodecOnce()
	{
		var previous = ReadCodec();
		WriteCodec(null);
		try
		{
			Sqid.Configure(new SqidOptions { MinLength = 6 });
			await Assert.That(Sqid.Codec.MinLength).IsEqualTo(6);
			await Assert.That(Sqid.From(1).ToString().Length).IsGreaterThanOrEqualTo(6);

			await Assert.That(() => Sqid.Configure(new SqidOptions())).Throws<InvalidOperationException>();
		}
		finally
		{
			WriteCodec(previous);
		}
	}

	[Test]
	public async Task Configure_InvalidOptions_ThrowsAndLeavesTheCodecUnset()
	{
		var previous = ReadCodec();
		WriteCodec(null);
		try
		{
			await Assert.That(() => Sqid.Configure(new SqidOptions { MinLength = 1000 })).Throws<ArgumentException>();
			await Assert.That(ReadCodec()).IsNull();
		}
		finally
		{
			WriteCodec(previous);
		}
	}

	[Test]
	public async Task Codec_IsCreatedLazilyAndStaysStable()
	{
		var previous = ReadCodec();
		WriteCodec(null);
		try
		{
			var first = Sqid.Codec;
			await Assert.That(first).IsSameReferenceAs(Sqid.Codec);
			await Assert.That(first.MinLength).IsEqualTo(0);
			await Assert.That(Sqid.From(1).ToString()).IsEqualTo("Uk");
		}
		finally
		{
			WriteCodec(previous);
		}
	}

	[Test]
	public async Task AddSqids_WithConfigure_ConfiguresTheSharedCodec()
	{
		var previous = ReadCodec();
		WriteCodec(null);
		try
		{
			var services = new ServiceCollection();
			services.AddSqids(o =>
			{
				o.Alphabet = "abcdef";
				o.MinLength = 5;
			});

			using var provider = services.BuildServiceProvider();
			var codec = provider.GetRequiredService<SqidCodec>();
			await Assert.That(codec.MinLength).IsEqualTo(5);
			await Assert.That(codec).IsSameReferenceAs(Sqid.Codec);
		}
		finally
		{
			WriteCodec(previous);
		}
	}

	private static SqidCodec? ReadCodec() => (SqidCodec?)CodecField.GetValue(null);

	private static void WriteCodec(SqidCodec? codec) => CodecField.SetValue(null, codec);
}
