using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.XtreamPro.Api.Json;

/// <summary>
/// Reads strings that may arrive as numbers. Empty strings become null.
/// </summary>
internal sealed class FlexibleStringConverter : JsonConverter<string?>
{
    public override bool HandleNull => true;

    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                var s = reader.GetString();
                return string.IsNullOrEmpty(s) ? null : s;
            case JsonTokenType.Number:
                return Encoding.UTF8.GetString(reader.ValueSpan);
            default:
                reader.Skip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value);
    }
}
