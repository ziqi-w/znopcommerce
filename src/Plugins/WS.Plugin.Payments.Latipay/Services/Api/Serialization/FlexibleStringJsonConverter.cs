using System.Text.Json;
using System.Text.Json.Serialization;

namespace WS.Plugin.Payments.Latipay.Services.Api.Serialization;

/// <summary>
/// Reads string-like JSON values without losing the provider's textual representation.
/// </summary>
public class FlexibleStringJsonConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => ReadNumericValue(ref reader),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            JsonTokenType.Null => null,
            _ => throw new JsonException($"Unexpected token '{reader.TokenType}' when parsing a flexible string value.")
        };
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }

    private static string ReadNumericValue(ref Utf8JsonReader reader)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return document.RootElement.GetRawText();
    }
}
