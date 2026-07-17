using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColorBlocks;

public static class LevelStorage
{
    public const string LevelId = "level1";
    public const string LevelFileName = "level.json";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static Level LoadOrCreateDefault()
    {
        foreach (string path in GetReadablePaths().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                string json = File.ReadAllText(path);
                LevelData data = JsonSerializer.Deserialize<LevelData>(json, JsonOptions);
                if (data is not null)
                {
                    return Level.FromData(data);
                }
            }
            catch (JsonException)
            {
            }
        }

        Level level = Level.CreateDefault();
        Save(level);
        return level;
    }

    public static void Save(Level level)
    {
        string path = GetWritablePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        string json = JsonSerializer.Serialize(level.ToData(), JsonOptions);
        File.WriteAllText(path, json);
    }

    public static string GetWritablePath()
    {
        return UserDataPaths.LegacyLevelSaveFile;
    }

    private static IEnumerable<string> GetReadablePaths()
    {
        yield return UserDataPaths.LegacyLevelSaveFile;
        yield return Path.Combine(AppContext.BaseDirectory, "Content", LevelFileName);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        JsonSerializerOptions options = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
