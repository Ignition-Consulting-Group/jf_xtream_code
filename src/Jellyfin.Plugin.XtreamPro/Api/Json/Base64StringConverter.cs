using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.XtreamPro.Api.Json;

/// <summary>
/// Decodes base64 text, as used by <c>get_short_epg</c>. Text that is not base64 is returned as is.
/// </summary>
internal sealed class Base64StringConverter : JsonConverter<string?>
{
    private readonly FlexibleStringConverter _inner = new();

    public override bool HandleNull => true;

    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var raw = _inner.Read(ref reader, typeToConvert, options);
        if (raw is null)
        {
            return null;
        }

        var buffer = new byte[raw.Length];
        return Convert.TryFromBase64String(raw, buffer, out var written) ? Encoding.UTF8.GetString(buffer, 0, written) : raw;
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value is null ? null : Convert.ToBase64String(Encoding.UTF8.GetBytes(value)));
    }
}
