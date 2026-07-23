#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ColorBlocks.Replay;

namespace ColorBlocks;

public static class LevelLibrary
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        DeveloperSettings.Reload();
        UserDataPaths.Initialize();
        EnsureContentFolders();
        _initialized = true;
    }

    public static IReadOnlyList<LevelMetadata> GetOfficialLevels() =>
        ListLevelsForSource(LevelSource.Official);

    public static IReadOnlyList<LevelMetadata> GetLocalLevels() =>
        ListLevelsForSource(LevelSource.Local);

    public static IReadOnlyList<LevelMetadata> GetWorkshopLevels() =>
        ListLevelsForSource(LevelSource.Workshop);

    public static IReadOnlyList<LevelMetadata> GetAllLevels()
    {
        Initialize();
        var combined = new List<LevelMetadata>();
        combined.AddRange(GetOfficialLevels());
        combined.AddRange(GetWorkshopLevels());
        combined.AddRange(GetLocalLevels());
        return combined;
    }

    public static IReadOnlyList<LevelMetadata> GetEditableLevels()
    {
        Initialize();
        var levels = new List<LevelMetadata>();
        levels.AddRange(GetLocalLevels());
        levels.AddRange(GetWorkshopLevels());
        if (DeveloperSettings.DeveloperMode)
        {
            levels.InsertRange(0, GetOfficialLevels());
        }

        return levels;
    }

    public static LevelMetadata? GetLevel(string levelId)
    {
        Initialize();
        if (!LevelIdentity.TryParse(levelId, out LevelSource source, out string fileStem))
        {
            return null;
        }

        return FindLevelMetadata(source, fileStem);
    }

    /// <summary>
    /// Next level in the same source list (display-name order, same as Level Select).
    /// </summary>
    public static bool TryGetNextLevelId(string currentLevelId, out string nextLevelId)
    {
        nextLevelId = string.Empty;
        Initialize();

        if (!LevelIdentity.TryParse(currentLevelId, out LevelSource source, out _))
        {
            return false;
        }

        IReadOnlyList<LevelMetadata> levels = ListLevelsForSource(source);
        int index = -1;
        for (int i = 0; i < levels.Count; i++)
        {
            if (string.Equals(levels[i].Id, currentLevelId, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        if (index < 0 || index >= levels.Count - 1)
        {
            return false;
        }

        nextLevelId = levels[index + 1].Id;
        return true;
    }

    public static Level LoadLevel(string levelId)
    {
        LevelMetadata? metadata = GetLevel(levelId);
        if (metadata is null)
        {
            return Level.CreateDefault();
        }

        try
        {
            if (File.Exists(metadata.FilePath))
            {
                string json = File.ReadAllText(metadata.FilePath);
                LevelData? data = JsonSerializer.Deserialize<LevelData>(json, JsonOptions);
                if (data is not null)
                {
                    return Level.FromData(data);
                }
            }
        }
        catch
        {
            // Fall back to default if loading fails.
        }

        return Level.CreateDefault();
    }

    public static bool CanSaveLevel(string levelId)
    {
        LevelMetadata? metadata = GetLevel(levelId);
        if (metadata is null)
        {
            return true;
        }

        if (metadata.Source == LevelSource.Official && !DeveloperSettings.DeveloperMode)
        {
            return false;
        }

        if (metadata.Source == LevelSource.Official
            && LevelContentPaths.TryGetProjectOfficialLevelsRoot() is null)
        {
            Console.WriteLine($"Save blocked: project OfficialLevels folder is unavailable for {levelId}.");
            return false;
        }

        if (metadata.Source == LevelSource.Workshop && !DeveloperSettings.DeveloperMode)
        {
            return false;
        }

        return true;
    }

    public static bool SaveLevel(Level level, string levelId)
    {
        if (!CanSaveLevel(levelId))
        {
            Console.WriteLine($"Save blocked for read-only level {levelId}.");
            return false;
        }

        LevelMetadata? metadata = GetLevel(levelId);
        if (metadata is null)
        {
            Console.WriteLine($"Save failed: unknown level {levelId}.");
            return false;
        }

        try
        {
            LevelData data = level.ToData();
            ApplyMetadataOnSave(data, metadata);
            string json = JsonSerializer.Serialize(data, JsonOptions);
            File.WriteAllText(metadata.FilePath, json);
            if (metadata.Source == LevelSource.Official)
            {
                SyncOfficialFileToProject(metadata.FilePath);
            }

            metadata.Name = data.Name;
            metadata.ModifiedDate = data.ModifiedDate ?? DateTime.UtcNow;
            metadata.Version = data.Version;
            metadata.Author = data.Author;
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving level {levelId}: {ex.Message}");
            return false;
        }
    }

    public static string CreateNewLevel(string displayName)
    {
        Initialize();
        var levelData = new LevelData
        {
            Name = displayName,
            PlayerSpawn = new Vector2Data { X = 0f, Y = 0f },
            Author = LevelAuthorProvider.GetLocalAuthor(),
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow,
            Version = 1
        };

        return SaveNewLocalLevel(Level.FromData(levelData), levelData.Name);
    }

    public static string DuplicateLevel(string sourceLevelId, string? desiredName = null)
    {
        Level source = LoadLevel(sourceLevelId);
        LevelData data = source.ToData();
        data.Name = desiredName ?? BuildCopyName(source.Name);
        data.Author = LevelAuthorProvider.GetLocalAuthor();
        data.WorkshopId = string.Empty;
        data.OwnerSteamId = string.Empty;
        data.DownloadedVersion = string.Empty;
        data.LastSync = null;
        data.CreatedDate = DateTime.UtcNow;
        data.ModifiedDate = DateTime.UtcNow;
        data.Version = 1;

        return SaveNewLocalLevel(Level.FromData(data), data.Name);
    }

    public static string ImportOfficialLevel(string officialLevelId)
    {
        LevelMetadata? metadata = GetLevel(officialLevelId);
        if (metadata is null || metadata.Source != LevelSource.Official)
        {
            throw new InvalidOperationException($"Level {officialLevelId} is not an official level.");
        }

        string copyName = BuildCopyName(metadata.Name);
        return DuplicateLevel(officialLevelId, copyName);
    }

    public static void DeleteLevel(string levelId)
    {
        LevelMetadata? metadata = GetLevel(levelId);
        if (metadata is null)
        {
            return;
        }

        if (metadata.Source == LevelSource.Official && !DeveloperSettings.DeveloperMode)
        {
            Console.WriteLine($"Delete blocked for official level {levelId}.");
            return;
        }

        if (metadata.Source == LevelSource.Workshop && !DeveloperSettings.DeveloperMode)
        {
            Console.WriteLine($"Delete blocked for workshop level {levelId}.");
            return;
        }

        try
        {
            if (metadata.Source == LevelSource.Workshop)
            {
                string workshopFolder = Path.GetDirectoryName(metadata.FilePath)!;
                if (Directory.Exists(workshopFolder))
                {
                    Directory.Delete(workshopFolder, recursive: true);
                }
            }
            else if (File.Exists(metadata.FilePath))
            {
                string deletedPath = metadata.FilePath;
                File.Delete(deletedPath);
                if (metadata.Source == LevelSource.Official)
                {
                    DeleteOfficialFileFromProject(deletedPath);
                }
            }

            BestTimeStorage.DeleteLevelRecord(levelId);
            ReplayStorage.DeleteBestReplay(levelId);
            SteamGhostService.InvalidateWorldRecordGhost(levelId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting level {levelId}: {ex.Message}");
        }
    }

    public static bool ConvertLocalToOfficial(string localLevelId)
    {
        if (!DeveloperSettings.DeveloperMode)
        {
            return false;
        }

        LevelMetadata? metadata = GetLevel(localLevelId);
        if (metadata is null || metadata.Source != LevelSource.Local)
        {
            return false;
        }

        string destinationStem = Path.GetFileNameWithoutExtension(metadata.FilePath);
        string destinationPath = GetUniqueLevelPath(LevelSource.Official, destinationStem);

        try
        {
            File.Move(metadata.FilePath, destinationPath);
            LevelData data = ReadLevelData(destinationPath) ?? new LevelData();
            data.Author = "Game";
            data.ModifiedDate = DateTime.UtcNow;
            WriteLevelData(destinationPath, data);
            SyncOfficialFileToProject(destinationPath);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ConvertLocalToOfficial failed for {localLevelId}: {ex.Message}");
            return false;
        }
    }

    public static string CreateOfficialLevel(string displayName)
    {
        if (!DeveloperSettings.DeveloperMode)
        {
            throw new InvalidOperationException("Developer mode is required to create official levels.");
        }

        string fileStem = SanitizeFileStem(displayName);
        string filePath = GetUniqueLevelPath(LevelSource.Official, fileStem);
        fileStem = Path.GetFileNameWithoutExtension(filePath);
        var levelData = new LevelData
        {
            Name = displayName,
            PlayerSpawn = new Vector2Data { X = 0f, Y = 0f },
            Author = "Game",
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow,
            Version = 1
        };

        WriteLevelData(filePath, levelData);
        SyncOfficialFileToProject(filePath);
        return LevelIdentity.Compose(LevelSource.Official, fileStem);
    }

    private static IReadOnlyList<LevelMetadata> ListLevelsForSource(LevelSource source)
    {
        Initialize();
        return source switch
        {
            LevelSource.Official => ListJsonLevels(source),
            LevelSource.Local => ListJsonLevels(source),
            LevelSource.Workshop => ListWorkshopLevels(),
            _ => Array.Empty<LevelMetadata>()
        };
    }

    private static List<LevelMetadata> ListJsonLevels(LevelSource source)
    {
        string root = GetLevelsRootForAccess(source);
        if (source != LevelSource.Official)
        {
            Directory.CreateDirectory(root);
        }

        var levels = new List<LevelMetadata>();
        if (!Directory.Exists(root))
        {
            return levels;
        }

        foreach (string filePath in Directory.GetFiles(root, "*.json").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            LevelMetadata? metadata = TryCreateMetadataFromJsonFile(source, filePath);
            if (metadata is not null)
            {
                levels.Add(metadata);
            }
        }

        SortLevelsByName(levels);
        return levels;
    }

    private static List<LevelMetadata> ListWorkshopLevels()
    {
        string root = LevelContentPaths.GetLevelsRoot(LevelSource.Workshop);
        if (!Directory.Exists(root))
        {
            return new List<LevelMetadata>();
        }

        var levels = new List<LevelMetadata>();
        foreach (string workshopFolder in Directory.GetDirectories(root).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            string workshopId = Path.GetFileName(workshopFolder);
            string levelPath = LevelContentPaths.GetWorkshopLevelFile(workshopId);
            if (!File.Exists(levelPath))
            {
                continue;
            }

            LevelMetadata? metadata = TryCreateMetadataFromJsonFile(LevelSource.Workshop, levelPath, workshopId);
            if (metadata is not null)
            {
                levels.Add(metadata);
            }
        }

        SortLevelsByName(levels);
        return levels;
    }

    private static void SortLevelsByName(List<LevelMetadata> levels)
    {
        levels.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
    }

    private static LevelMetadata? TryCreateMetadataFromJsonFile(
        LevelSource source,
        string filePath,
        string? workshopIdOverride = null)
    {
        try
        {
            string fileStem = workshopIdOverride ?? Path.GetFileNameWithoutExtension(filePath);
            string levelId = LevelIdentity.Compose(source, fileStem);
            LevelData? data = ReadLevelData(filePath);
            string displayName = data is not null && !string.IsNullOrWhiteSpace(data.Name)
                ? data.Name
                : fileStem;

            DateTime created = data?.CreatedDate ?? File.GetCreationTimeUtc(filePath);
            DateTime modified = data?.ModifiedDate ?? File.GetLastWriteTimeUtc(filePath);

            return new LevelMetadata
            {
                Id = levelId,
                Name = displayName,
                FilePath = filePath,
                Source = source,
                Author = LevelAuthorProvider.GetAuthorForSource(source, data),
                WorkshopId = source == LevelSource.Workshop
                    ? (string.IsNullOrWhiteSpace(data?.WorkshopId) ? fileStem : data.WorkshopId)
                    : string.Empty,
                CreatedDate = created,
                ModifiedDate = modified,
                Version = data?.Version ?? 1,
                OwnerSteamId = data?.OwnerSteamId ?? string.Empty,
                DownloadedVersion = data?.DownloadedVersion ?? string.Empty,
                LastSync = data?.LastSync
            };
        }
        catch
        {
            return null;
        }
    }

    private static LevelMetadata? FindLevelMetadata(LevelSource source, string fileStem)
    {
        if (source == LevelSource.Workshop)
        {
            string workshopPath = LevelContentPaths.GetWorkshopLevelFile(fileStem);
            if (File.Exists(workshopPath))
            {
                return TryCreateMetadataFromJsonFile(source, workshopPath, fileStem);
            }

            return null;
        }

        string filePath = Path.Combine(GetLevelsRootForAccess(source), $"{fileStem}.json");
        if (!File.Exists(filePath))
        {
            return null;
        }

        return TryCreateMetadataFromJsonFile(source, filePath);
    }

    private static string SaveNewLocalLevel(Level level, string preferredName)
    {
        string fileStem = SanitizeFileStem(preferredName);
        string filePath = GetUniqueLevelPath(LevelSource.Local, fileStem);
        fileStem = Path.GetFileNameWithoutExtension(filePath);

        LevelData data = level.ToData();
        data.Author = LevelAuthorProvider.GetLocalAuthor();
        data.CreatedDate = DateTime.UtcNow;
        data.ModifiedDate = DateTime.UtcNow;
        data.Version = 1;
        WriteLevelData(filePath, data);

        return LevelIdentity.Compose(LevelSource.Local, fileStem);
    }

    private static string GetUniqueLevelPath(LevelSource source, string fileStem)
    {
        string root = source == LevelSource.Official
            ? LevelContentPaths.TryGetProjectOfficialLevelsRoot()
                ?? throw new InvalidOperationException("Project OfficialLevels folder is unavailable.")
            : LevelContentPaths.GetLevelsRoot(source);
        Directory.CreateDirectory(root);

        string candidateStem = fileStem;
        int suffix = 1;
        while (File.Exists(Path.Combine(root, $"{candidateStem}.json")))
        {
            candidateStem = $"{fileStem}_{suffix}";
            suffix++;
        }

        return Path.Combine(root, $"{candidateStem}.json");
    }

    private static void ApplyMetadataOnSave(LevelData data, LevelMetadata metadata)
    {
        if (metadata.Source == LevelSource.Official)
        {
            data.Author = "Game";
        }
        else if (metadata.Source == LevelSource.Local)
        {
            if (string.IsNullOrWhiteSpace(data.Author))
            {
                data.Author = LevelAuthorProvider.GetLocalAuthor();
            }

            // Level.ToData() does not carry workshop fields; keep a published local
            // level linked to its workshop item across editor saves.
            if (string.IsNullOrWhiteSpace(data.WorkshopId))
            {
                data.WorkshopId = metadata.WorkshopId;
            }

            if (string.IsNullOrWhiteSpace(data.OwnerSteamId))
            {
                data.OwnerSteamId = metadata.OwnerSteamId;
            }

            data.LastSync ??= metadata.LastSync;
        }

        data.CreatedDate ??= metadata.CreatedDate == default ? DateTime.UtcNow : metadata.CreatedDate;
        data.ModifiedDate = DateTime.UtcNow;
        data.Version = Math.Max(1, metadata.Version + 1);
        if (metadata.Source == LevelSource.Workshop && string.IsNullOrWhiteSpace(data.WorkshopId))
        {
            data.WorkshopId = metadata.WorkshopId;
        }
    }

    private static string BuildCopyName(string sourceName)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
        {
            return "Copy";
        }

        string trimmed = sourceName.TrimEnd();
        if (trimmed.EndsWith(" Copy", StringComparison.OrdinalIgnoreCase)
            || trimmed.EndsWith(" (Copy)", StringComparison.OrdinalIgnoreCase))
        {
            return $"{trimmed} 2";
        }

        return $"{trimmed} Copy";
    }

    private static string SanitizeFileStem(string name)
    {
        string stem = LevelIdentity.ToFileSafeStem(name.Trim());
        if (string.IsNullOrWhiteSpace(stem))
        {
            stem = "Level";
        }

        return stem.Replace(' ', '_');
    }

    private static LevelData? ReadLevelData(string filePath)
    {
        string json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<LevelData>(json, JsonOptions);
    }

    private static void WriteLevelData(string filePath, LevelData data)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    private static void SyncOfficialFileToProject(string runtimeFilePath)
    {
        string? projectOfficialRoot = LevelContentPaths.TryGetProjectOfficialLevelsRoot();
        if (projectOfficialRoot is null || !File.Exists(runtimeFilePath))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(projectOfficialRoot);
            string destination = Path.Combine(projectOfficialRoot, Path.GetFileName(runtimeFilePath));
            if (string.Equals(
                Path.GetFullPath(runtimeFilePath),
                Path.GetFullPath(destination),
                StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            File.Copy(runtimeFilePath, destination, overwrite: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to sync official level to project Content: {ex.Message}");
        }
    }

    private static void DeleteOfficialFileFromProject(string runtimeFilePath)
    {
        string? projectOfficialRoot = LevelContentPaths.TryGetProjectOfficialLevelsRoot();
        if (projectOfficialRoot is null)
        {
            return;
        }

        try
        {
            string destination = Path.Combine(projectOfficialRoot, Path.GetFileName(runtimeFilePath));
            if (File.Exists(destination))
            {
                File.Delete(destination);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to delete official level from project Content: {ex.Message}");
        }
    }

    private static void EnsureContentFolders()
    {
        Directory.CreateDirectory(LevelContentPaths.GetLevelsRoot(LevelSource.Local));
        Directory.CreateDirectory(LevelContentPaths.GetLevelsRoot(LevelSource.Workshop));
        Directory.CreateDirectory(LevelContentPaths.GetReplaysRoot(LevelSource.Official));
        Directory.CreateDirectory(LevelContentPaths.GetReplaysRoot(LevelSource.Local));
        Directory.CreateDirectory(LevelContentPaths.GetReplaysRoot(LevelSource.Workshop));
        Directory.CreateDirectory(LevelContentPaths.GetPreviewsRoot(LevelSource.Official));
        Directory.CreateDirectory(LevelContentPaths.GetPreviewsRoot(LevelSource.Local));
        Directory.CreateDirectory(LevelContentPaths.GetPreviewsRoot(LevelSource.Workshop));
        Directory.CreateDirectory(UserDataPaths.BestTimes);
    }

    private static string GetLevelsRootForAccess(LevelSource source)
    {
        if (source == LevelSource.Official
            && DeveloperSettings.DeveloperMode
            && LevelContentPaths.TryGetProjectOfficialLevelsRoot() is string projectRoot)
        {
            return projectRoot;
        }

        return LevelContentPaths.GetLevelsRoot(source);
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
