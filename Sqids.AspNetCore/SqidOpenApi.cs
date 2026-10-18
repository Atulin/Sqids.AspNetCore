using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Sqids.AspNetCore;

public static class SqidOpenApiExtensions
{
    extension(OpenApiOptions options)
    {
        /// <summary>
        /// Describes <see cref="Sqid"/> (and <c>Sqid?</c>) as a constrained string in the generated OpenAPI document,
        /// for properties, request/response bodies and route/query/header parameters alike.
        /// Written against the Microsoft.OpenApi 2.x object model used by Microsoft.AspNetCore.OpenApi.
        /// </summary>
        public OpenApiOptions AddSqidSchema()
        {
            return options.AddSchemaTransformer(static (schema, context, _) =>
            {
                if (!DescribesSqid(context))
                {
                    return Task.CompletedTask;
                }

                var codec = Sqid.Codec;
                var nullable = schema.Type is { } existing && existing.HasFlag(JsonSchemaType.Null);

                schema.Type = nullable ? JsonSchemaType.String | JsonSchemaType.Null : JsonSchemaType.String;
                schema.Format = "sqid";
                schema.Pattern = $"^[{CharacterClass(codec.AlphabetCharacters)}]+$";
                schema.MinLength = codec.MinIdLength;
                schema.MaxLength = codec.MaxIdLength;
                schema.Description = "Opaque identifier. Only the canonical form is accepted.";
                schema.Examples = [ JsonValue.Create(Sqid.From(1).ToString()) ];
                return Task.CompletedTask;
            });
        }
    }

    // Properties, bodies and JSON-serialized values reach the transformer as a Sqid JsonTypeInfo.
    // Route/query/header parameters do not: depending on whether the endpoint is a Minimal API or an
    // MVC action, the declared type only survives in ParameterDescription.Type or
    // ParameterDescription.ModelMetadata.ModelType (both surface as System.String), so all three are consulted.
    private static bool DescribesSqid(OpenApiSchemaTransformerContext context)
    {
        if (IsSqid(context.JsonTypeInfo.Type))
        {
            return true;
        }

        var parameter = context.ParameterDescription;
        return parameter is not null && (IsSqid(parameter.Type) || IsSqid(parameter.ModelMetadata?.ModelType));

    }

    /// <summary>
    /// Determines whether the specified <see cref="Type"/> represents a <see cref="Sqid"/> or a nullable <see cref="Sqid"/>.
    /// </summary>
    /// <param name="type">The type to evaluate.</param>
    /// <returns>
    /// <c>true</c> if the specified type is a <see cref="Sqid"/> or a nullable <see cref="Sqid"/>; otherwise, <c>false</c>.
    /// </returns>
    private static bool IsSqid(Type? type) => type is not null && (Nullable.GetUnderlyingType(type) ?? type) == typeof(Sqid);

    // Builds an ECMA-262 character class body from the sorted alphabet, compressing runs like a-z and 0-9.
    // Only the sorted *set* is published, never the secret shuffled order.
    private static string CharacterClass(string sortedChars)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < sortedChars.Length;)
        {
            var j = i;
            while (j + 1 < sortedChars.Length && sortedChars[j + 1] == sortedChars[j] + 1)
            {
                j++;
            }

            if (j - i >= 2)
            {
                Append(sb, sortedChars[i]);
                sb.Append('-');
                Append(sb, sortedChars[j]);
            }
            else
            {
                for (var k = i; k <= j; k++)
                {
                    Append(sb, sortedChars[k]);
                }
            }

            i = j + 1;
        }

        return sb.ToString();

        static void Append(StringBuilder sb, char c)
        {
            if (c is '\\' or ']' or '^' or '-')
            {
                sb.Append('\\');
            }

            sb.Append(c);
        }
    }
}
