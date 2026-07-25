#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColorBlocks;

/// <summary>
/// Reads SoundEffects as float volumes. Migrates legacy bool values (true→1, false→0).
/// </summary>
public sealed class SoundEffectVolumeDictionaryConverter : JsonConverter<Dictionary<string, float>>
{
    public override Dictionary<string, float> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected SoundEffects object.");
        }

        var result = new Dictionary<string, float>(StringComparer.Ordinal);
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return result;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected SoundEffects property name.");
            }

            string key = reader.GetString() ?? string.Empty;
            reader.Read();
            result[key] = ReadVolume(ref reader);
        }

        throw new JsonException("Unexpected end of SoundEffects object.");
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, float> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (KeyValuePair<string, float> pair in value)
        {
            writer.WriteNumber(pair.Key, pair.Value);
        }

        writer.WriteEndObject();
    }

    private static float ReadVolume(ref Utf8JsonReader reader)
    {
        return reader.TokenType switch
        {
            JsonTokenType.True => 1f,
            JsonTokenType.False => 0f,
            JsonTokenType.Number => Math.Clamp(reader.GetSingle(), 0f, 1f),
            _ => 1f
        };
    }
}
