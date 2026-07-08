#nullable enable
using System;
using System.IO;

namespace ColorBlocks;

internal static class LevelContentPaths
{
    public const string LegacyLevelsFolder = "Levels";
    public const string OfficialLevelsFolder = "OfficialLevels";
    public const string UserLevelsFolder = "UserLevels";
    public const string WorkshopLevelsFolder = "WorkshopLevels";
    public const string ReplaysFolder = "Replays";
    public const string PreviewsFolder = "LevelPreviews";
    public const string BestTimesFolder = "BestTimes";
    public const string MigrationMarkerFile = ".level_migration_v1";

    public static string ContentRoot => Path.Combine(AppContext.BaseDirectory, "Content");

    public static string GetLevelsRoot(LevelSource source) =>
        source switch
        {
            LevelSource.Official => Path.Combine(ContentRoot, OfficialLevelsFolder),
            LevelSource.Local => Path.Combine(ContentRoot, UserLevelsFolder),
            LevelSource.Workshop => Path.Combine(ContentRoot, WorkshopLevelsFolder),
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null)
        };

    public static string GetLegacyLevelsRoot() =>
        Path.Combine(ContentRoot, LegacyLevelsFolder);

    public static string GetReplaysRoot(LevelSource source) =>
        Path.Combine(ContentRoot, ReplaysFolder, source.ToString());

    public static string GetPreviewsRoot(LevelSource source) =>
        Path.Combine(ContentRoot, PreviewsFolder, source.ToString());

    public static string GetBestTimesPath(LevelSource source) =>
        Path.Combine(ContentRoot, BestTimesFolder, $"{source}.json");

    public static string GetLegacyBestTimesPath() =>
        Path.Combine(AppContext.BaseDirectory, BestTimeStorage.BestTimesFileName);

    public static string GetLegacyReplaysRoot() =>
        Path.Combine(ContentRoot, ReplaysFolder);

    public static string GetLegacyPreviewsRoot() =>
        Path.Combine(ContentRoot, PreviewsFolder);

    public static string GetWorkshopLevelFile(string workshopId) =>
        Path.Combine(GetLevelsRoot(LevelSource.Workshop), workshopId, "level.json");

    public static string GetWorkshopPreviewFile(string workshopId) =>
        Path.Combine(GetLevelsRoot(LevelSource.Workshop), workshopId, "preview.png");
}
