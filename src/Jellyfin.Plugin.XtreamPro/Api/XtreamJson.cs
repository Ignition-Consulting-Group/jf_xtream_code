using System.Text.Json;
using Jellyfin.Plugin.XtreamPro.Api.Json;

namespace Jellyfin.Plugin.XtreamPro.Api;

/// <summary>
/// JSON settings that tolerate the quirks of Xtream providers.
/// </summary>
public static class XtreamJson
{
    /// <summary>
    /// Gets the serializer options for provider responses.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new FlexibleNumberConverter<int>(),
            new FlexibleNumberConverter<long>(),
            new FlexibleNumberConverter<double>(),
            new RequiredNumberConverter<int>(),
            new FlexibleBoolConverter(),
            new FlexibleStringConverter(),
            new UnixTimeConverter(),
            new ListOrEmptyObjectConverterFactory(),
        },
    };

    /// <summary>
    /// Deserializes a provider response.
    /// </summary>
    /// <typeparam name="T">Target type.</typeparam>
    /// <param name="json">Response body.</param>
    /// <returns>The parsed value.</returns>
    public static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options)!;
}
