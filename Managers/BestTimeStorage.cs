using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ColorBlocks.Replay;

namespace ColorBlocks;

public static class BestTimeStorage
{
    public const string BestTimesFileName = "best_times.json";

    private sealed class LevelBestTimesRecord
    {
        public float? Official { get; set; }
        public float? Unofficial { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static bool SaveIfRecord(string levelId, float elapsedSeconds)
    {
        Dictionary<string, LevelBestTimesRecord> bestTimes = LoadAll();
        float roundedTime = RoundToCentiseconds(elapsedSeconds);

        if (!bestTimes.TryGetValue(levelId, out LevelBestTimesRecord? record))
        {
            record = new LevelBestTimesRecord();
            bestTimes[levelId] = record;
        }

        if (record.Official is float savedBest && roundedTime >= savedBest)
        {
            return false;
        }

        record.Official = roundedTime;
        SaveAll(bestTimes);
        return true;
    }

    public static bool TryGetBestTime(string levelId, out float bestTime)
    {
        bestTime = 0f;
        if (!LoadAll().TryGetValue(levelId, out LevelBestTimesRecord? record) || record.Official is not float official)
        {
            return false;
        }

        bestTime = official;
        return true;
    }

    public static bool TryGetUnofficialBestTime(string levelId, out float unofficialBestTime)
    {
        unofficialBestTime = 0f;
        if (!LoadAll().TryGetValue(levelId, out LevelBestTimesRecord? record) || record.Unofficial is not float unofficial)
        {
            return false;
        }

        unofficialBestTime = unofficial;
        return true;
    }

    public static void InvalidateOfficialOnLevelEdit(string levelId)
    {
        Dictionary<string, LevelBestTimesRecord> bestTimes = LoadAll();
        if (!bestTimes.TryGetValue(levelId, out LevelBestTimesRecord? record) || record.Official is not float official)
        {
            return;
        }

        if (record.Unofficial is float unofficial)
        {
            record.Unofficial = MathF.Min(official, unofficial);
        }
        else
        {
            record.Unofficial = official;
        }

        record.Official = null;
        SaveAll(bestTimes);
        ReplayInvalidation.OnLevelEdited(levelId);
    }

    public static void DeleteLevelRecord(string levelId)
    {
        Dictionary<string, LevelBestTimesRecord> bestTimes = LoadAll();
        if (bestTimes.Remove(levelId))
        {
            SaveAll(bestTimes);
        }
    }

    public static float RoundToCentiseconds(float elapsedSeconds)
    {
        return MathF.Floor(MathF.Max(0f, elapsedSeconds) * 100f) / 100f;
    }

    public static string FormatTime(float seconds)
    {
        TimeSpan ts = TimeSpan.FromSeconds(seconds);
        return $"{ts.Minutes:00}:{ts.Seconds:00}:{(int)(ts.Milliseconds / 10):00}";
    }

    private static Dictionary<string, LevelBestTimesRecord> LoadAll()
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
                Dictionary<string, LevelBestTimesRecord>? bestTimes = DeserializeRecords(json);
                if (bestTimes is not null)
                {
                    return bestTimes;
                }
            }
            catch (JsonException)
            {
            }
            catch (IOException)
            {
            }
        }

        return new Dictionary<string, LevelBestTimesRecord>();
    }

    private static Dictionary<string, LevelBestTimesRecord>? DeserializeRecords(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var records = new Dictionary<string, LevelBestTimesRecord>();
        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Number)
            {
                records[property.Name] = new LevelBestTimesRecord
                {
                    Official = property.Value.GetSingle()
                };
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                LevelBestTimesRecord? record = JsonSerializer.Deserialize<LevelBestTimesRecord>(
                    property.Value.GetRawText(),
                    JsonOptions);
                if (record is not null)
                {
                    records[property.Name] = record;
                }
            }
        }

        return records;
    }

    private static void SaveAll(Dictionary<string, LevelBestTimesRecord> bestTimes)
    {
        string path = GetWritablePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        string json = JsonSerializer.Serialize(bestTimes, JsonOptions);
        File.WriteAllText(path, json);
    }

    private static string GetWritablePath()
    {
        return Path.Combine(AppContext.BaseDirectory, BestTimesFileName);
    }

    private static IEnumerable<string> GetReadablePaths()
    {
        yield return Path.Combine(AppContext.BaseDirectory, BestTimesFileName);
        yield return Path.Combine(Environment.CurrentDirectory, BestTimesFileName);
    }
}
