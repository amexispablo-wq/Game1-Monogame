#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ColorBlocks;

public static class DeveloperSettings
{
    private const string SettingsFileName = "developer_settings.json";
    private static bool _initialized;
    private static bool _developerMode;

    public static bool DeveloperMode
    {
        get
        {
            EnsureInitialized();
            return _developerMode;
        }
    }

    public static void Reload()
    {
        _initialized = false;
        EnsureInitialized();
        if (!_developerMode)
        {
            NavigationDebug.Enabled = false;
        }
    }

    private static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        _developerMode = false;
        string? path = FindSettingsPath();
        if (path is null)
        {
            _initialized = true;
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            DeveloperSettingsFile? settings = JsonSerializer.Deserialize<DeveloperSettingsFile>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            _developerMode = settings?.DeveloperMode == true;
        }
        catch
        {
            _developerMode = false;
        }

        if (!_developerMode)
        {
            NavigationDebug.Enabled = false;
        }

        _initialized = true;
    }

    private static string? FindSettingsPath()
    {
        foreach (string path in GetSearchPaths())
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetSearchPaths()
    {
        string? directory = AppContext.BaseDirectory;
        yield return Path.Combine(directory, SettingsFileName);

        for (int i = 0; i < 5 && directory is not null; i++)
        {
            DirectoryInfo? parent = Directory.GetParent(directory);
            if (parent is null)
            {
                break;
            }

            directory = parent.FullName;
            yield return Path.Combine(directory, SettingsFileName);
        }
    }

    private sealed class DeveloperSettingsFile
    {
        public bool DeveloperMode { get; set; }
    }
}
