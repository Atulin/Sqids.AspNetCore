using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sqids;

/// <summary>
/// Reads and writes <see cref="Sqid"/> as a JSON string (also as a dictionary key). Zero-copy on the happy path:
/// the UTF-8 token bytes are decoded in place and the ID is written from a stack buffer.
/// Numbers are rejected on purpose: the wire format is always the canonical Sqid text.
/// </summary>
public sealed class SqidJsonConverter : JsonConverter<Sqid>
{
    public override Sqid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) 
	    => reader.TokenType == JsonTokenType.String 
		    ? ReadCore(ref reader) 
		    : throw new JsonException($"Expected a Sqid string but found a JSON {reader.TokenType} token.");

    public override Sqid ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => ReadCore(ref reader);

    public override void Write(Utf8JsonWriter writer, Sqid value, JsonSerializerOptions options)
    {
        Span<byte> buffer = stackalloc byte[Sqid.Codec.MaxIdLength];
        Sqid.Codec.TryEncode(value.Value, buffer, out var written);
        writer.WriteStringValue(buffer[..written]);
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, Sqid value, JsonSerializerOptions options)
    {
        Span<byte> buffer = stackalloc byte[Sqid.Codec.MaxIdLength];
        Sqid.Codec.TryEncode(value.Value, buffer, out var written);
        writer.WritePropertyName(buffer[..written]);
    }

    private static Sqid ReadCore(ref Utf8JsonReader reader)
    {
        Sqid result;
        SqidParseError error;

        if (!reader.HasValueSequence && !reader.ValueIsEscaped)
        {
            _ = Sqid.TryParse(reader.ValueSpan, out result, out error);
        }
        else
        {
            // Escaped or split across buffers: canonical IDs never need this, so take the simple, allocating path.
            var text = reader.GetString() ?? string.Empty;
            _ = Sqid.TryParse(text.AsSpan(), out result, out error);
        }

        return error == SqidParseError.None 
	        ? result 
	        : throw new JsonException($"Invalid Sqid: {SqidCodec.Describe(error)}");
    }
}

/// <summary>
/// Marks <see cref="Sqid"/> as a convertible simple type so MVC and Razor Pages treat it as a scalar
/// (no complex-type binding or validation traversal) and <c>Microsoft.Extensions.Configuration</c> can bind it.
/// </summary>
public sealed class SqidTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string);

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) => destinationType == typeof(string);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        => value is string s 
	        ? Sqid.Parse(s, culture) 
	        : base.ConvertFrom(context, culture, value);

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        => destinationType == typeof(string) && value is Sqid sqid 
	        ? sqid.ToString() 
	        : base.ConvertTo(context, culture, value, destinationType);
}
