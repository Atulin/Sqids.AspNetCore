using System.ComponentModel;
using System.Globalization;

namespace Sqids.AspNetCore.Tests;

public sealed class SqidTypeConverterTests
{
	[Test]
	public async Task IsRegisteredAsTheTypeConverter()
	{
		await Assert.That(TypeDescriptor.GetConverter(typeof(Sqid))).IsTypeOf<SqidTypeConverter>();
	}

	[Test]
	public async Task CanConvert_OnlyStrings()
	{
		var converter = new SqidTypeConverter();

		await Assert.That(converter.CanConvertFrom(typeof(string))).IsTrue();
		await Assert.That(converter.CanConvertFrom(typeof(int))).IsFalse();
		await Assert.That(converter.CanConvertTo(typeof(string))).IsTrue();
		await Assert.That(converter.CanConvertTo(typeof(int))).IsFalse();
	}

	[Test]
	public async Task ConvertFrom_String_ParsesTheSqid()
	{
		var converter = new SqidTypeConverter();
		var value = converter.ConvertFrom(null, CultureInfo.InvariantCulture, "Uk");
		await Assert.That(value).IsEqualTo(Sqid.From(1));
	}

	[Test]
	public async Task ConvertFrom_NonString_Throws()
	{
		var converter = new SqidTypeConverter();
		await Assert.That(() => converter.ConvertFrom(null, CultureInfo.InvariantCulture, 5)).Throws<NotSupportedException>();
	}

	[Test]
	public async Task ConvertTo_String_FormatsTheSqid()
	{
		var converter = new SqidTypeConverter();
		var value = converter.ConvertTo(null, CultureInfo.InvariantCulture, Sqid.From(1), typeof(string));
		await Assert.That(value).IsEqualTo("Uk");
	}

	[Test]
	public async Task ConvertTo_WrongDestination_Throws()
	{
		var converter = new SqidTypeConverter();
		await Assert.That(() => converter.ConvertTo(null, CultureInfo.InvariantCulture, Sqid.From(1), typeof(int))).Throws<NotSupportedException>();
	}
}
