using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gaia.Api.Contracts;

/// <summary>
/// Lets callers send Field A / Field B as JSON text ("7") or as a JSON number (7).
/// A number is kept exactly as written ("2.50" stays "2.50"), then parsed by the operation like any text.
/// </summary>
public sealed class TextOrNumberConverter : JsonConverter<string?>
{
    public override bool HandleNull => true;

    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
    {
        JsonTokenType.String => reader.GetString(),
        JsonTokenType.Number => Encoding.UTF8.GetString(reader.ValueSpan),
        JsonTokenType.Null => null,
        _ => throw new JsonException("Expected text or a number."),
    };

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
