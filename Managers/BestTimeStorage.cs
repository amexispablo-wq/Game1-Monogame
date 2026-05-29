using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ColorBlocks;

public static class BestTimeStorage
{
    public const string BestTimesFileName = "best_times.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static bool SaveIfRecord(string levelId, float elapsedSeconds)
    {
        Dictionary<string, float> bestTimes = LoadAll();
        float roundedTime = RoundToCentiseconds(elapsedSeconds);

        if (bestTimes.TryGetValue(levelId, out float savedBest) && roundedTime >= savedBest)
        {
            return false;
        }

        bestTimes[levelId] = roundedTime;
        SaveAll(bestTimes);
        return true;
    }

    public static bool TryGetBestTime(string levelId, out float bestTime)
    {
        return LoadAll().TryGetValue(levelId, out bestTime);
    }

    public static void ResetLevelRecord(string levelId)
    {
        Dictionary<string, float> bestTimes = LoadAll();
        if (bestTimes.ContainsKey(levelId))
        {
            bestTimes.Remove(levelId);
            SaveAll(bestTimes);
        }
    }

    public static void DeleteLevelRecord(string levelId)
    {
        ResetLevelRecord(levelId);
    }

    public static float RoundToCentiseconds(float elapsedSeconds)
    {
        return MathF.Floor(MathF.Max(0f, elapsedSeconds) * 100f) / 100f;
    }

    private static Dictionary<string, float> LoadAll()
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
                Dictionary<string, float> bestTimes = JsonSerializer.Deserialize<Dictionary<string, float>>(json, JsonOptions);
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

        return new Dictionary<string, float>();
    }

    private static void SaveAll(Dictionary<string, float> bestTimes)
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
