#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ColorBlocks.Replay;

namespace ColorBlocks;

public static class LevelManager
{
    private const string LevelsFolder = "Levels";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static IReadOnlyList<LevelMetadata> GetAllLevels()
    {
        string levelsPath = GetLevelsDirectory();
        Directory.CreateDirectory(levelsPath);

        var levels = new List<LevelMetadata>();
        var files = Directory.GetFiles(levelsPath, "level_*.json")
            .OrderBy(f => ExtractLevelNumber(f))
            .ToList();

        foreach (string filePath in files)
        {
            try
            {
                string id = Path.GetFileNameWithoutExtension(filePath);
                string displayName = GetDisplayName(id);
                string json = File.ReadAllText(filePath);
                LevelData? data = JsonSerializer.Deserialize<LevelData>(json, JsonOptions);
                if (data != null && !string.IsNullOrWhiteSpace(data.Name))
                {
                    displayName = data.Name;
                }

                levels.Add(new LevelMetadata
                {
                    Id = id,
                    Name = displayName,
                    FilePath = filePath
                });
            }
            catch
            {
                // Skip invalid level files
            }
        }

        return levels;
    }

    public static LevelMetadata? GetLevel(string levelId)
    {
        var levels = GetAllLevels();
        return levels.FirstOrDefault(l => l.Id == levelId);
    }

    public static Level LoadLevel(string levelId)
    {
        var metadata = GetLevel(levelId);
        if (metadata == null)
        {
            return Level.CreateDefault();
        }

        try
        {
            if (File.Exists(metadata.FilePath))
            {
                string json = File.ReadAllText(metadata.FilePath);
                LevelData? data = JsonSerializer.Deserialize<LevelData>(json, JsonOptions);
                if (data != null)
                {
                    return Level.FromData(data);
                }
            }
        }
        catch
        {
            // Fall back to default if loading fails
        }

        return Level.CreateDefault();
    }

    public static void SaveLevel(Level level, string levelId)
    {
        string levelsPath = GetLevelsDirectory();
        Directory.CreateDirectory(levelsPath);

        string fileName = $"{levelId}.json";
        string filePath = Path.Combine(levelsPath, fileName);

        try
        {
            string json = JsonSerializer.Serialize(level.ToData(), JsonOptions);
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving level {levelId}: {ex.Message}");
        }
    }

    public static string CreateNewLevel(string displayName)
    {
        string levelsPath = GetLevelsDirectory();
        Directory.CreateDirectory(levelsPath);

        int nextNumber = 1;
        while (File.Exists(Path.Combine(levelsPath, $"level_{nextNumber}.json")))
        {
            nextNumber++;
        }

        string levelId = $"level_{nextNumber}";
        var levelData = new LevelData
        {
            Name = displayName,
            PlayerSpawn = new Vector2Data { X = 0f, Y = 0f }
        };

        Level emptyLevel = Level.FromData(levelData);
        SaveLevel(emptyLevel, levelId);

        return levelId;
    }

    public static void DeleteLevel(string levelId)
    {
        var metadata = GetLevel(levelId);
        if (metadata != null && File.Exists(metadata.FilePath))
        {
            try
            {
                File.Delete(metadata.FilePath);
                BestTimeStorage.DeleteLevelRecord(levelId);
                ReplayStorage.DeleteBestReplay(levelId);
                HighlightManager.InvalidateClipsForLevel(levelId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting level {levelId}: {ex.Message}");
            }
        }
    }

    public static void RenameLevel(string levelId, string newDisplayName)
    {
        if (string.IsNullOrWhiteSpace(newDisplayName))
        {
            return;
        }

        Level level = LoadLevel(levelId);
        level.Name = newDisplayName.Trim();
        SaveLevel(level, levelId);
    }

    private static string GetLevelsDirectory()
    {
        return Path.Combine(AppContext.BaseDirectory, "Content", LevelsFolder);
    }

    private static string GetDisplayName(string levelId)
    {
        // Convert "level_1" -> "Level 1"
        if (levelId.StartsWith("level_"))
        {
            string numberPart = levelId.Substring("level_".Length);
            if (int.TryParse(numberPart, out int number))
            {
                return $"Level {number}";
            }
        }

        return levelId;
    }

    private static int ExtractLevelNumber(string filePath)
    {
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        if (fileName.StartsWith("level_"))
        {
            string numberPart = fileName.Substring("level_".Length);
            if (int.TryParse(numberPart, out int number))
            {
                return number;
            }
        }

        return int.MaxValue;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
