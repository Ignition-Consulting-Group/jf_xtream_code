using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.XtreamPro.Api.Json;

/// <summary>
/// Reads unix timestamps sent as numbers or strings.
/// </summary>
internal sealed class UnixTimeConverter : JsonConverter<DateTimeOffset?>
{
    private readonly FlexibleNumberConverter<long> _seconds = new();

    public override bool HandleNull => true;

    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => _seconds.Read(ref reader, typeof(long?), options) is { } s and > 0 ? DateTimeOffset.FromUnixTimeSeconds(s) : null;

    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
        => _seconds.Write(writer, value?.ToUnixTimeSeconds(), options);
}
