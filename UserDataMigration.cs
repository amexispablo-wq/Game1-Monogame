#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using ColorBlocks.Replay;

namespace ColorBlocks;

public static class UserDataMigration
{
    private static readonly List<string> Messages = new();

    public static string Status { get; private set; } = "Not run";

    public static void RunIfNeeded()
    {
        UserDataPaths.Initialize();

        if (File.Exists(UserDataPaths.MigrationMarker))
        {
            Status = "Complete";
            return;
        }

        Messages.Clear();
        Status = "Running";
        Log($"Migration started. Source: {AppContext.BaseDirectory}");

        try
        {
            MigrateInstallData();
            File.WriteAllLines(UserDataPaths.MigrationMarker, new[]
            {
                DateTime.UtcNow.ToString("O"),
                $"Migrated entries: {Messages.Count}"
            });
            Status = "Complete";
            Log("Migration complete.");
        }
        catch (Exception ex)
        {
            Status = $"Failed: {ex.Message}";
            Log($"Migration failed: {ex}");
        }
    }

    private static void MigrateInstallData()
    {
        string installRoot = AppContext.BaseDirectory;
        string contentRoot = Path.Combine(installRoot, "Content");
        string currentRoot = Environment.CurrentDirectory;

        MigrateNewestFile(Path.Combine(contentRoot, "settings.json"), UserDataPaths.SettingsFile);
        MigrateNewestFile(Path.Combine(installRoot, "settings.json"), UserDataPaths.SettingsFile);
        MigrateNewestFile(Path.Combine(currentRoot, "Content", "settings.json"), UserDataPaths.SettingsFile);
        MigrateNewestFile(Path.Combine(currentRoot, "settings.json"), UserDataPaths.SettingsFile);
        MigrateNewestFile(Path.Combine(contentRoot, "skin_library.json"), UserDataPaths.SkinLibraryFile);
        MigrateNewestFile(Path.Combine(installRoot, LevelStorage.LevelFileName), UserDataPaths.LegacyLevelSaveFile);
        MigrateNewestFile(Path.Combine(currentRoot, LevelStorage.LevelFileName), UserDataPaths.LegacyLevelSaveFile);

        MigrateDirectory(Path.Combine(contentRoot, "UserLevels"), UserDataPaths.UserLevels);
        MigrateDirectory(Path.Combine(contentRoot, "Levels"), UserDataPaths.UserLevels);
        MigrateDirectory(Path.Combine(contentRoot, "WorkshopLevels"), UserDataPaths.Workshop);
        MigrateDirectory(Path.Combine(contentRoot, "Workshop"), UserDataPaths.Workshop);
        MigrateDirectory(Path.Combine(contentRoot, "BestTimes"), UserDataPaths.BestTimes);
        MigrateDirectory(Path.Combine(contentRoot, "Ghosts"), UserDataPaths.Ghosts);
        MigrateDirectory(Path.Combine(contentRoot, "Highlights"), UserDataPaths.Highlights);
        MigrateLegacyPreviewTree(Path.Combine(contentRoot, "LevelPreviews"));
        MigrateDirectory(Path.Combine(contentRoot, "Skins"), UserDataPaths.Skins);

        MigrateLegacyReplayTree(Path.Combine(contentRoot, "Replays"));

        string legacyBestTimes = Path.Combine(installRoot, BestTimeStorage.BestTimesFileName);
        foreach (string path in new[]
        {
            legacyBestTimes,
            Path.Combine(currentRoot, BestTimeStorage.BestTimesFileName)
        })
        {
            if (!File.Exists(path))
            {
                continue;
            }

            BestTimeStorage.ImportLegacyBestTimesFile(path);
            MigrateNewestFile(
                path,
                Path.Combine(UserDataPaths.Saves, "Legacy", BestTimeStorage.BestTimesFileName));
        }

        MigrateDirectory(
            Path.Combine(installRoot, "Developer", "GameplayBenchmark"),
            UserDataPaths.Benchmarks);
        MigrateDirectory(
            Path.Combine(installRoot, "Developer", "FuzzFailures"),
            UserDataPaths.FuzzFailures);
        MigrateDirectory(Path.Combine(installRoot, "Screenshots"), UserDataPaths.Screenshots);
        MigrateDirectory(Path.Combine(installRoot, "Logs"), UserDataPaths.Logs);
        MigrateDirectory(Path.Combine(installRoot, "Cache"), UserDataPaths.Cache);
        MigrateDirectory(Path.Combine(installRoot, "Temporary"), UserDataPaths.Temporary);
        MigrateDirectory(Path.Combine(installRoot, "Temp"), UserDataPaths.Temporary);
    }

    private static void MigrateLegacyReplayTree(string sourceRoot)
    {
        if (!Directory.Exists(sourceRoot))
        {
            return;
        }

        foreach (string sourcePath in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceRoot, sourcePath);
            string[] segments = relativePath.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);

            LevelSource source = LevelSource.Local;
            int fileSegmentIndex = 0;
            if (segments.Length > 1 && Enum.TryParse(segments[0], ignoreCase: true, out LevelSource parsedSource))
            {
                source = parsedSource;
                fileSegmentIndex = 1;
            }

            string trailingPath = Path.Combine(segments[fileSegmentIndex..]);
            string fileName = Path.GetFileName(sourcePath);
            string destinationRoot;

            if (string.Equals(fileName, ReplayStorage.HighlightsFileName, StringComparison.OrdinalIgnoreCase))
            {
                destinationRoot = UserDataPaths.GetHighlightsRoot(source);
            }
            else if (fileName.EndsWith("_Best.replay", StringComparison.OrdinalIgnoreCase))
            {
                destinationRoot = UserDataPaths.GetGhostsRoot(source);
            }
            else
            {
                destinationRoot = UserDataPaths.GetReplaysRoot(source);
            }

            MigrateNewestFile(sourcePath, Path.Combine(destinationRoot, trailingPath));
        }

        TryDeleteEmptyDirectoryTree(sourceRoot);
    }

    private static void MigrateLegacyPreviewTree(string sourceRoot)
    {
        if (!Directory.Exists(sourceRoot))
        {
            return;
        }

        foreach (string sourcePath in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceRoot, sourcePath);
            string[] segments = relativePath.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);

            LevelSource source = LevelSource.Local;
            int fileSegmentIndex = 0;
            if (segments.Length > 1 && Enum.TryParse(segments[0], ignoreCase: true, out LevelSource parsedSource))
            {
                source = parsedSource;
                fileSegmentIndex = 1;
            }

            string trailingPath = Path.Combine(segments[fileSegmentIndex..]);
            MigrateNewestFile(
                sourcePath,
                Path.Combine(UserDataPaths.GetLevelPreviewsRoot(source), trailingPath));
        }

        TryDeleteEmptyDirectoryTree(sourceRoot);
    }

    private static void MigrateDirectory(string sourceRoot, string destinationRoot)
    {
        if (!Directory.Exists(sourceRoot))
        {
            return;
        }

        foreach (string sourcePath in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceRoot, sourcePath);
            MigrateNewestFile(sourcePath, Path.Combine(destinationRoot, relativePath));
        }

        TryDeleteEmptyDirectoryTree(sourceRoot);
    }

    private static void MigrateNewestFile(string sourcePath, string destinationPath)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        try
        {
            string? destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            bool destinationExists = File.Exists(destinationPath);
            DateTime sourceWriteTime = File.GetLastWriteTimeUtc(sourcePath);
            DateTime destinationWriteTime = destinationExists
                ? File.GetLastWriteTimeUtc(destinationPath)
                : DateTime.MinValue;

            if (!destinationExists || sourceWriteTime > destinationWriteTime)
            {
                File.Copy(sourcePath, destinationPath, overwrite: destinationExists);
                File.SetLastWriteTimeUtc(destinationPath, sourceWriteTime);
                Log($"Migrated: {sourcePath} -> {destinationPath}");
            }
            else
            {
                Log($"Kept newer destination: {destinationPath}");
            }

            TryDeleteSourceFile(sourcePath);
        }
        catch (Exception ex)
        {
            Log($"Could not migrate {sourcePath}: {ex.Message}");
        }
    }

    private static void TryDeleteSourceFile(string sourcePath)
    {
        try
        {
            File.Delete(sourcePath);
        }
        catch (Exception ex)
        {
            Log($"Copied but could not remove source {sourcePath}: {ex.Message}");
        }
    }

    private static void TryDeleteEmptyDirectoryTree(string root)
    {
        try
        {
            if (!Directory.Exists(root))
            {
                return;
            }

            string[] directories = Directory.GetDirectories(root, "*", SearchOption.AllDirectories);
            Array.Sort(directories, (left, right) => right.Length.CompareTo(left.Length));
            foreach (string directory in directories)
            {
                if (Directory.GetFileSystemEntries(directory).Length == 0)
                {
                    Directory.Delete(directory);
                }
            }

            if (Directory.GetFileSystemEntries(root).Length == 0)
            {
                Directory.Delete(root);
                Log($"Removed migrated directory: {root}");
            }
        }
        catch (Exception ex)
        {
            Log($"Could not remove migrated directory {root}: {ex.Message}");
        }
    }

    private static void Log(string message)
    {
        string entry = $"[{DateTime.UtcNow:O}] {message}";
        Messages.Add(entry);
        Console.WriteLine(entry);

        try
        {
            Directory.CreateDirectory(UserDataPaths.Logs);
            File.AppendAllText(UserDataPaths.MigrationLog, entry + Environment.NewLine);
        }
        catch
        {
            // Migration must not fail because logging is unavailable.
        }
    }
}
