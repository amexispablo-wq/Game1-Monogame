#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ColorBlocks.Replay;

namespace ColorBlocks;

internal static class LevelMigration
{
    public static void RunIfNeeded()
    {
        string markerPath = Path.Combine(LevelContentPaths.ContentRoot, LevelContentPaths.MigrationMarkerFile);
        if (File.Exists(markerPath))
        {
            return;
        }

        Directory.CreateDirectory(LevelContentPaths.ContentRoot);
        MigrateLegacyLevels();
        MigrateLegacyBestTimes();
        MigrateLegacyReplays();
        MigrateLegacyPreviews();
        MigrateLegacyHighlights();

        File.WriteAllText(markerPath, DateTime.UtcNow.ToString("O"));
    }

    private static void MigrateLegacyLevels()
    {
        string legacyRoot = LevelContentPaths.GetLegacyLevelsRoot();
        if (!Directory.Exists(legacyRoot))
        {
            return;
        }

        string userRoot = LevelContentPaths.GetLevelsRoot(LevelSource.Local);
        Directory.CreateDirectory(userRoot);

        foreach (string filePath in Directory.GetFiles(legacyRoot, "*.json"))
        {
            string fileName = Path.GetFileName(filePath);
            string destination = Path.Combine(userRoot, fileName);
            if (File.Exists(destination))
            {
                continue;
            }

            try
            {
                File.Move(filePath, destination);
            }
            catch
            {
                try
                {
                    File.Copy(filePath, destination, overwrite: false);
                }
                catch
                {
                    // Keep going; player data is best-effort.
                }
            }
        }
    }

    private static void MigrateLegacyBestTimes()
    {
        foreach (string legacyPath in GetLegacyBestTimePaths())
        {
            BestTimeStorage.ImportLegacyBestTimesFile(legacyPath);
        }
    }

    private static void MigrateLegacyReplays()
    {
        string legacyRoot = LevelContentPaths.GetLegacyReplaysRoot();
        if (!Directory.Exists(legacyRoot))
        {
            return;
        }

        foreach (string replayPath in Directory.GetFiles(legacyRoot, "*_Best.replay"))
        {
            string fileName = Path.GetFileNameWithoutExtension(replayPath);
            if (!fileName.EndsWith("_Best", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string legacyLevelId = fileName[..^"_Best".Length];
            string normalizedId = LevelIdentity.NormalizeLegacyId(legacyLevelId);
            LevelSource source = LevelIdentity.GetSource(normalizedId);
            string destinationDirectory = LevelContentPaths.GetReplaysRoot(source);
            Directory.CreateDirectory(destinationDirectory);
            string destination = Path.Combine(destinationDirectory, $"{SanitizeReplayFileName(normalizedId)}_Best.replay");
            if (File.Exists(destination))
            {
                continue;
            }

            TryMoveFile(replayPath, destination);
        }
    }

    private static void MigrateLegacyPreviews()
    {
        string legacyRoot = LevelContentPaths.GetLegacyPreviewsRoot();
        if (!Directory.Exists(legacyRoot))
        {
            return;
        }

        foreach (string previewPath in Directory.EnumerateFiles(legacyRoot, "*.png"))
        {
            string fileName = Path.GetFileNameWithoutExtension(previewPath);
            string? legacyLevelId = TryExtractLegacyLevelIdFromPreview(fileName);
            if (legacyLevelId is null)
            {
                continue;
            }

            string normalizedId = LevelIdentity.NormalizeLegacyId(legacyLevelId);
            LevelSource source = LevelIdentity.GetSource(normalizedId);
            string destinationDirectory = LevelContentPaths.GetPreviewsRoot(source);
            Directory.CreateDirectory(destinationDirectory);
            string destination = Path.Combine(destinationDirectory, Path.GetFileName(previewPath));
            if (File.Exists(destination))
            {
                continue;
            }

            TryMoveFile(previewPath, destination);
        }
    }

    private static void MigrateLegacyHighlights()
    {
        string legacyHighlights = Path.Combine(LevelContentPaths.GetLegacyReplaysRoot(), ReplayStorage.HighlightsFileName);
        if (!File.Exists(legacyHighlights))
        {
            return;
        }

        string destinationDirectory = LevelContentPaths.GetReplaysRoot(LevelSource.Local);
        Directory.CreateDirectory(destinationDirectory);
        string destination = Path.Combine(destinationDirectory, ReplayStorage.HighlightsFileName);
        if (File.Exists(destination))
        {
            return;
        }

        TryMoveFile(legacyHighlights, destination);
    }

    private static IEnumerable<string> GetLegacyBestTimePaths()
    {
        yield return LevelContentPaths.GetLegacyBestTimesPath();
        yield return Path.Combine(Environment.CurrentDirectory, BestTimeStorage.BestTimesFileName);
    }

    private static string? TryExtractLegacyLevelIdFromPreview(string fileName)
    {
        int separatorIndex = fileName.LastIndexOf('_');
        if (separatorIndex <= 0 || separatorIndex >= fileName.Length - 1)
        {
            return fileName.Contains("level_", StringComparison.OrdinalIgnoreCase) ? fileName : null;
        }

        return fileName[(separatorIndex + 1)..];
    }

    private static string SanitizeReplayFileName(string levelId) =>
        levelId.Replace(':', '_');

    private static void TryMoveFile(string sourcePath, string destinationPath)
    {
        try
        {
            File.Move(sourcePath, destinationPath);
        }
        catch
        {
            try
            {
                File.Copy(sourcePath, destinationPath, overwrite: false);
            }
            catch
            {
                // Ignore migration copy failures.
            }
        }
    }
}
