using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.XtreamPro.Api.Json;

/// <summary>
/// Reads booleans sent as <c>true</c>, <c>1</c> or <c>"1"</c>. Anything else is false.
/// </summary>
internal sealed class FlexibleBoolConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.True:
                return true;
            case JsonTokenType.Number:
                return reader.TryGetInt64(out var n) && n != 0;
            case JsonTokenType.String:
                var s = reader.GetString();
                return s == "1" || string.Equals(s, "true", StringComparison.OrdinalIgnoreCase);
            default:
                reader.Skip();
                return false;
        }
    }

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteBooleanValue(value);
    }
}
