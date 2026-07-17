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
            LevelSource.Local => UserDataPaths.GetUserLevelsRoot(),
            LevelSource.Workshop => UserDataPaths.GetWorkshopRoot(),
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null)
        };

    public static string GetLegacyLevelsRoot() =>
        Path.Combine(ContentRoot, LegacyLevelsFolder);

    public static string GetReplaysRoot(LevelSource source) =>
        UserDataPaths.GetReplaysRoot(source);

    public static string GetPreviewsRoot(LevelSource source) =>
        UserDataPaths.GetLevelPreviewsRoot(source);

    public static string GetBestTimesPath(LevelSource source) =>
        UserDataPaths.GetBestTimesPath(source);

    public static string GetLegacyBestTimesPath() =>
        Path.Combine(AppContext.BaseDirectory, BestTimeStorage.BestTimesFileName);

    public static string GetLegacyReplaysRoot() =>
        Path.Combine(ContentRoot, ReplaysFolder);

    public static string GetLegacyPreviewsRoot() =>
        Path.Combine(ContentRoot, PreviewsFolder);

    public static string GetWorkshopLevelFile(string workshopId) =>
        UserDataPaths.GetWorkshopLevelFile(workshopId);

    public static string GetWorkshopPreviewFile(string workshopId) =>
        UserDataPaths.GetWorkshopPreviewFile(workshopId);

    /// <summary>
    /// Project-source OfficialLevels folder (not bin output). Used so developer
    /// create/delete/save of official levels survive rebuild CopyToOutputDirectory.
    /// </summary>
    public static string? TryGetProjectOfficialLevelsRoot()
    {
        string runtimeOfficial = Path.GetFullPath(GetLevelsRoot(LevelSource.Official));
        string? directory = AppContext.BaseDirectory;

        for (int i = 0; i < 8 && directory is not null; i++)
        {
            string candidate = Path.Combine(directory, "Content", OfficialLevelsFolder);
            if (Directory.Exists(candidate))
            {
                string full = Path.GetFullPath(candidate);
                if (!string.Equals(full, runtimeOfficial, StringComparison.OrdinalIgnoreCase))
                {
                    return full;
                }
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        return null;
    }
}
