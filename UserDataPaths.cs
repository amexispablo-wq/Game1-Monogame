#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

namespace ColorBlocks;

/// <summary>
/// Central location for every player-generated or writable game file.
/// Shipped content must continue to use paths under AppContext.BaseDirectory.
/// </summary>
public static class UserDataPaths
{
    private const string ProductFolderName = "Color Blocks";
    private static readonly object InitializationLock = new();
    private static bool _initialized;

    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ProductFolderName);

    public static string Settings => Path.Combine(Root, "Settings");
    public static string Saves => Path.Combine(Root, "Saves");
    public static string UserLevels => Path.Combine(Root, "UserLevels");
    public static string Workshop => Path.Combine(Root, "Workshop");
    public static string BestTimes => Path.Combine(Root, "BestTimes");
    public static string Ghosts => Path.Combine(Root, "Ghosts");
    public static string Replays => Path.Combine(Root, "Replays");
    public static string Highlights => Path.Combine(Root, "Highlights");
    public static string Screenshots => Path.Combine(Root, "Screenshots");
    public static string Benchmarks => Path.Combine(Root, "Benchmarks");
    public static string Logs => Path.Combine(Root, "Logs");
    public static string Skins => Path.Combine(Root, "Skins");
    public static string Cache => Path.Combine(Root, "Cache");
    public static string Temporary => Path.Combine(Root, "Temporary");

    public static string SettingsFile => Path.Combine(Settings, "settings.json");
    public static string LegacyLevelSaveFile => Path.Combine(Saves, LevelStorage.LevelFileName);
    public static string SkinLibraryFile => Path.Combine(Skins, "skin_library.json");
    public static string LevelPreviews => Path.Combine(Cache, "LevelPreviews");
    public static string ReplayCache => Path.Combine(Cache, "Replays");
    public static string BenchmarkSettingsFile => Path.Combine(Benchmarks, "settings.json");
    public static string BenchmarkReportFile => Path.Combine(Benchmarks, "GameplayBenchmarkReport.json");
    public static string FuzzFailures => Path.Combine(Benchmarks, "FuzzFailures");
    public static string MigrationMarker => Path.Combine(Root, "userdata_migration_v1");
    public static string MigrationLog => Path.Combine(Logs, "userdata_migration_v1.log");

    public static string GetUserLevelsRoot() => UserLevels;

    public static string GetWorkshopRoot() => Workshop;

    public static string GetBestTimesPath(LevelSource source) =>
        Path.Combine(BestTimes, $"{source}.json");

    public static string GetGhostsRoot(LevelSource source) =>
        Path.Combine(Ghosts, source.ToString());

    public static string GetReplaysRoot(LevelSource source) =>
        Path.Combine(Replays, source.ToString());

    public static string GetHighlightsRoot(LevelSource source) =>
        Path.Combine(Highlights, source.ToString());

    public static string GetLevelPreviewsRoot(LevelSource source) =>
        Path.Combine(LevelPreviews, source.ToString());

    public static string GetWorkshopLevelFile(string workshopId) =>
        Path.Combine(Workshop, workshopId, "level.json");

    public static string GetWorkshopPreviewFile(string workshopId) =>
        Path.Combine(Workshop, workshopId, "preview.png");

    public static string GetFuzzFailureRoot(int seed) =>
        Path.Combine(FuzzFailures, $"Seed_{seed:D8}");

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        lock (InitializationLock)
        {
            if (_initialized)
            {
                return;
            }

            foreach (string directory in GetRequiredDirectories())
            {
                Directory.CreateDirectory(directory);
            }

            _initialized = true;
        }
    }

    private static IEnumerable<string> GetRequiredDirectories()
    {
        yield return Root;
        yield return Settings;
        yield return Saves;
        yield return UserLevels;
        yield return Workshop;
        yield return BestTimes;
        yield return Ghosts;
        yield return Replays;
        yield return Highlights;
        yield return Screenshots;
        yield return Benchmarks;
        yield return Logs;
        yield return Skins;
        yield return Cache;
        yield return Temporary;
        yield return LevelPreviews;
        yield return ReplayCache;

        foreach (LevelSource source in Enum.GetValues<LevelSource>())
        {
            yield return GetGhostsRoot(source);
            yield return GetReplaysRoot(source);
            yield return GetHighlightsRoot(source);
            yield return GetLevelPreviewsRoot(source);
        }
    }
}
