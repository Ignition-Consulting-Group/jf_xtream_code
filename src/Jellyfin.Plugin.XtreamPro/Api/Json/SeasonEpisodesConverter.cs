using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.XtreamPro.Api.Models;

namespace Jellyfin.Plugin.XtreamPro.Api.Json;

/// <summary>
/// Reads episodes sent either as an object keyed by season or as an array of season arrays.
/// </summary>
internal sealed class SeasonEpisodesConverter : JsonConverter<IReadOnlyDictionary<int, IReadOnlyList<Episode>>>
{
    public override IReadOnlyDictionary<int, IReadOnlyList<Episode>> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.StartObject:
                var bySeason = JsonSerializer.Deserialize<Dictionary<string, List<Episode>>>(ref reader, options) ?? [];
                return bySeason
                    .Where(s => int.TryParse(s.Key, CultureInfo.InvariantCulture, out _))
                    .ToDictionary(s => int.Parse(s.Key, CultureInfo.InvariantCulture), s => (IReadOnlyList<Episode>)s.Value);
            case JsonTokenType.StartArray:
                var seasons = JsonSerializer.Deserialize<List<List<Episode>>>(ref reader, options) ?? [];
                return seasons
                    .SelectMany(s => s)
                    .GroupBy(e => e.Season ?? 1)
                    .ToDictionary(g => g.Key, g => (IReadOnlyList<Episode>)g.ToList());
            default:
                reader.Skip();
                return new Dictionary<int, IReadOnlyList<Episode>>();
        }
    }

    public override void Write(Utf8JsonWriter writer, IReadOnlyDictionary<int, IReadOnlyList<Episode>> value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, options);
}
