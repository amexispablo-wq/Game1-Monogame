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
    private const int MaxPlayerCounts = 4;

    private sealed class PlayerCountBestTimes
    {
        public float? Official { get; set; }
        public float? Unofficial { get; set; }
    }

    private sealed class LevelBestTimesRecord
    {
        /// <summary>Legacy flat official best (treated as 1P on read).</summary>
        public float? Official { get; set; }

        /// <summary>Legacy flat unofficial best (treated as 1P on read).</summary>
        public float? Unofficial { get; set; }

        /// <summary>Per player-count bests. Keys are "1".."4".</summary>
        public Dictionary<string, PlayerCountBestTimes>? ByPlayerCount { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static int ClampPlayerCount(int playerCount) =>
        Math.Clamp(playerCount, 1, MaxPlayerCounts);

    public static bool SaveIfRecord(string levelId, float elapsedSeconds, bool official, int playerCount)
    {
        int clamped = ClampPlayerCount(playerCount);
        LevelSource source = LevelIdentity.GetSource(levelId);
        Dictionary<string, LevelBestTimesRecord> bestTimes = LoadAll(source);
        float roundedTime = RoundToCentiseconds(elapsedSeconds);

        if (!bestTimes.TryGetValue(levelId, out LevelBestTimesRecord? record))
        {
            record = new LevelBestTimesRecord();
            bestTimes[levelId] = record;
        }

        PlayerCountBestTimes slot = GetOrCreatePlayerSlot(record, clamped);
        if (official)
        {
            if (TryResolveOfficial(record, clamped) is float savedBest && roundedTime >= savedBest)
            {
                return false;
            }

            slot.Official = roundedTime;
            // Keep legacy 1P field in sync for older readers.
            if (clamped == 1)
            {
                record.Official = roundedTime;
            }
        }
        else
        {
            if (TryResolveUnofficial(record, clamped) is float savedUnofficial && roundedTime >= savedUnofficial)
            {
                return false;
            }

            slot.Unofficial = roundedTime;
            if (clamped == 1)
            {
                record.Unofficial = roundedTime;
            }
        }

        SaveAll(source, bestTimes);
        return true;
    }

    public static bool TryGetBestTime(string levelId, int playerCount, out float bestTime)
    {
        bestTime = 0f;
        LevelSource source = LevelIdentity.GetSource(levelId);
        if (!LoadAll(source).TryGetValue(levelId, out LevelBestTimesRecord? record))
        {
            return false;
        }

        if (TryResolveOfficial(record, ClampPlayerCount(playerCount)) is not float official)
        {
            return false;
        }

        bestTime = official;
        return true;
    }

    public static bool TryGetUnofficialBestTime(string levelId, int playerCount, out float unofficialBestTime)
    {
        unofficialBestTime = 0f;
        LevelSource source = LevelIdentity.GetSource(levelId);
        if (!LoadAll(source).TryGetValue(levelId, out LevelBestTimesRecord? record))
        {
            return false;
        }

        if (TryResolveUnofficial(record, ClampPlayerCount(playerCount)) is not float unofficial)
        {
            return false;
        }

        unofficialBestTime = unofficial;
        return true;
    }

    public static void InvalidateOfficialOnLevelEdit(string levelId)
    {
        LevelSource source = LevelIdentity.GetSource(levelId);
        Dictionary<string, LevelBestTimesRecord> bestTimes = LoadAll(source);
        if (bestTimes.TryGetValue(levelId, out LevelBestTimesRecord? record))
        {
            bool changed = DemoteOfficialSlot(record);
            if (record.ByPlayerCount is not null)
            {
                foreach (string key in record.ByPlayerCount.Keys.ToList())
                {
                    changed |= DemoteOfficialInPlayerSlot(record.ByPlayerCount[key]);
                }
            }

            if (changed)
            {
                SaveAll(source, bestTimes);
            }
        }

        // Always invalidate replays/WR ghosts — content changed even when no official PB exists.
        ReplayInvalidation.OnLevelEdited(levelId);
    }

    public static void DeleteLevelRecord(string levelId)
    {
        LevelSource source = LevelIdentity.GetSource(levelId);
        Dictionary<string, LevelBestTimesRecord> bestTimes = LoadAll(source);
        if (bestTimes.Remove(levelId))
        {
            SaveAll(source, bestTimes);
        }
    }

    internal static void ImportLegacyBestTimesFile(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            Dictionary<string, LevelBestTimesRecord>? records = DeserializeRecords(json);
            if (records is null || records.Count == 0)
            {
                return;
            }

            var grouped = new Dictionary<LevelSource, Dictionary<string, LevelBestTimesRecord>>();
            foreach ((string legacyLevelId, LevelBestTimesRecord record) in records)
            {
                string normalizedId = LevelIdentity.NormalizeLegacyId(legacyLevelId);
                LevelSource source = LevelIdentity.GetSource(normalizedId);
                if (!grouped.TryGetValue(source, out Dictionary<string, LevelBestTimesRecord>? bucket))
                {
                    bucket = new Dictionary<string, LevelBestTimesRecord>(StringComparer.OrdinalIgnoreCase);
                    grouped[source] = bucket;
                }

                bucket[normalizedId] = record;
            }

            foreach ((LevelSource source, Dictionary<string, LevelBestTimesRecord> bucket) in grouped)
            {
                string destinationPath = GetWritablePath(source);
                if (File.Exists(destinationPath)
                    && File.GetLastWriteTimeUtc(destinationPath) >= File.GetLastWriteTimeUtc(path))
                {
                    continue;
                }

                Dictionary<string, LevelBestTimesRecord> existing = LoadAll(source);
                foreach ((string levelId, LevelBestTimesRecord record) in bucket)
                {
                    existing[levelId] = record;
                }

                SaveAll(source, existing);
            }
        }
        catch (JsonException)
        {
        }
        catch (IOException)
        {
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

    private static PlayerCountBestTimes GetOrCreatePlayerSlot(LevelBestTimesRecord record, int playerCount)
    {
        record.ByPlayerCount ??= new Dictionary<string, PlayerCountBestTimes>(StringComparer.Ordinal);
        string key = playerCount.ToString();
        if (!record.ByPlayerCount.TryGetValue(key, out PlayerCountBestTimes? slot))
        {
            slot = new PlayerCountBestTimes();
            record.ByPlayerCount[key] = slot;
        }

        return slot;
    }

    private static float? TryResolveOfficial(LevelBestTimesRecord record, int playerCount)
    {
        string key = playerCount.ToString();
        if (record.ByPlayerCount is not null
            && record.ByPlayerCount.TryGetValue(key, out PlayerCountBestTimes? slot)
            && slot.Official is float perCount)
        {
            return perCount;
        }

        // Legacy flat Official only applies to 1P.
        if (playerCount == 1 && record.Official is float legacy)
        {
            return legacy;
        }

        return null;
    }

    private static float? TryResolveUnofficial(LevelBestTimesRecord record, int playerCount)
    {
        string key = playerCount.ToString();
        if (record.ByPlayerCount is not null
            && record.ByPlayerCount.TryGetValue(key, out PlayerCountBestTimes? slot)
            && slot.Unofficial is float perCount)
        {
            return perCount;
        }

        if (playerCount == 1 && record.Unofficial is float legacy)
        {
            return legacy;
        }

        return null;
    }

    private static bool DemoteOfficialSlot(LevelBestTimesRecord record)
    {
        if (record.Official is not float official)
        {
            return false;
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
        return true;
    }

    private static bool DemoteOfficialInPlayerSlot(PlayerCountBestTimes slot)
    {
        if (slot.Official is not float official)
        {
            return false;
        }

        if (slot.Unofficial is float unofficial)
        {
            slot.Unofficial = MathF.Min(official, unofficial);
        }
        else
        {
            slot.Unofficial = official;
        }

        slot.Official = null;
        return true;
    }

    private static Dictionary<string, LevelBestTimesRecord> LoadAll(LevelSource source)
    {
        foreach (string path in GetReadablePaths(source).Distinct(StringComparer.OrdinalIgnoreCase))
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

    private static void SaveAll(LevelSource source, Dictionary<string, LevelBestTimesRecord> bestTimes)
    {
        string path = GetWritablePath(source);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        string json = JsonSerializer.Serialize(bestTimes, JsonOptions);
        File.WriteAllText(path, json);
    }

    private static string GetWritablePath(LevelSource source)
    {
        return LevelContentPaths.GetBestTimesPath(source);
    }

    private static IEnumerable<string> GetReadablePaths(LevelSource source)
    {
        yield return LevelContentPaths.GetBestTimesPath(source);
    }
}
