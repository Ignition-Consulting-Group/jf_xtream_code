using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.XtreamPro.Api.Json;

/// <summary>
/// Reads lists that providers send as <c>{}</c> or <c>null</c> when empty.
/// </summary>
internal sealed class ListOrEmptyObjectConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsGenericType
            && (typeToConvert.GetGenericTypeDefinition() == typeof(List<>) || typeToConvert.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            && typeToConvert.GenericTypeArguments[0] != typeof(string);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);
        var converter = typeof(Converter<,>).MakeGenericType(typeToConvert, typeToConvert.GenericTypeArguments[0]);
        return (JsonConverter)Activator.CreateInstance(converter)!;
    }

    private sealed class Converter<TCollection, T> : JsonConverter<TCollection>
        where TCollection : class, IEnumerable<T>
    {
        public override bool HandleNull => true;

        public override TCollection Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var list = new List<T>();
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                reader.Skip();
                return (TCollection)(object)list;
            }

            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (JsonSerializer.Deserialize<T>(ref reader, options) is { } item)
                {
                    list.Add(item);
                }
            }

            return (TCollection)(object)list;
        }

        public override void Write(Utf8JsonWriter writer, TCollection value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);
            writer.WriteStartArray();
            foreach (var item in value)
            {
                JsonSerializer.Serialize(writer, item, options);
            }

            writer.WriteEndArray();
        }
    }
}
