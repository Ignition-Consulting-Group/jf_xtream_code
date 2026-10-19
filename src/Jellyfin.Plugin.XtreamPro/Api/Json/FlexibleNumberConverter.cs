using System;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.XtreamPro.Api.Json;

/// <summary>
/// Reads numbers sent as numbers or strings. Empty or unparseable values become null.
/// </summary>
/// <typeparam name="T">Numeric type.</typeparam>
internal sealed class FlexibleNumberConverter<T> : JsonConverter<T?>
    where T : struct, INumber<T>
{
    public override bool HandleNull => true;

    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                return Parse(Encoding.UTF8.GetString(reader.ValueSpan));
            case JsonTokenType.String:
                return Parse(reader.GetString());
            case JsonTokenType.True:
                return T.One;
            case JsonTokenType.False:
                return T.Zero;
            default:
                reader.Skip();
                return null;
        }
    }

    private static T? Parse(string? text)
        => T.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

    public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (value is { } v)
        {
            writer.WriteRawValue(v.ToString(null, CultureInfo.InvariantCulture));
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
