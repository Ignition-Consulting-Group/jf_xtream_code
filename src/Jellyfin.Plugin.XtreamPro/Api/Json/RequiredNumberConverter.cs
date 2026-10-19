using System;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.XtreamPro.Api.Json;

/// <summary>
/// Non-nullable variant of <see cref="FlexibleNumberConverter{T}"/>. Missing values read as zero.
/// </summary>
/// <typeparam name="T">Numeric type.</typeparam>
internal sealed class RequiredNumberConverter<T> : JsonConverter<T>
    where T : struct, INumber<T>
{
    private readonly FlexibleNumberConverter<T> _inner = new();

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => _inner.Read(ref reader, typeToConvert, options) ?? T.Zero;

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        => _inner.Write(writer, value, options);
}
