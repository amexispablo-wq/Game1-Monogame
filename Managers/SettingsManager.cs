#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Game1_Monogame;

public static class SettingsManager
{
    private static GameSettings _currentSettings = new();
    private static GameSettings _pendingSettings = new();
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private const string SettingsFileName = "settings.json";

    public static GameSettings CurrentSettings => _currentSettings;
    public static GameSettings PendingSettings => _pendingSettings;

    public static void Initialize()
    {
        LoadSettings();
        _pendingSettings = new GameSettings
        {
            DisplayMode = _currentSettings.DisplayMode,
            ResolutionWidth = _currentSettings.ResolutionWidth,
            ResolutionHeight = _currentSettings.ResolutionHeight,
            MusicVolume = _currentSettings.MusicVolume,
            Keybindings = new Dictionary<string, string>(_currentSettings.Keybindings)
        };
    }

    public static void LoadSettings()
    {
        foreach (string path in GetReadablePaths())
        {
            if (!File.Exists(path))
                continue;

            try
            {
                string json = File.ReadAllText(path);
                GameSettings? loaded = JsonSerializer.Deserialize<GameSettings>(json, JsonOptions);
                if (loaded != null)
                {
                    _currentSettings = NormalizeSettings(loaded);
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
            }
        }

        _currentSettings = NormalizeSettings(new GameSettings());
    }

    public static void SaveSettings(GameSettings settings)
    {
        _currentSettings = NormalizeSettings(new GameSettings
        {
            DisplayMode = settings.DisplayMode,
            ResolutionWidth = settings.ResolutionWidth,
            ResolutionHeight = settings.ResolutionHeight,
            MusicVolume = settings.MusicVolume,
            Keybindings = new Dictionary<string, string>(settings.Keybindings)
        });

        try
        {
            string path = GetWritablePath();
            string directory = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(directory);

            string json = JsonSerializer.Serialize(_currentSettings, JsonOptions);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving settings: {ex.Message}");
        }
    }

    public static void RevertPendingChanges()
    {
        _pendingSettings = new GameSettings
        {
            DisplayMode = _currentSettings.DisplayMode,
            ResolutionWidth = _currentSettings.ResolutionWidth,
            ResolutionHeight = _currentSettings.ResolutionHeight,
            MusicVolume = _currentSettings.MusicVolume,
            Keybindings = new Dictionary<string, string>(_currentSettings.Keybindings)
        };
    }

    public static float GetMusicVolume() => _currentSettings.MusicVolume;
    public static string GetKeybinding(string actionName) => _currentSettings.Keybindings.TryGetValue(actionName, out var key) ? key : "UNBOUND";

    private static GameSettings NormalizeSettings(GameSettings settings)
    {
        settings.Keybindings ??= new Dictionary<string, string>();

        bool migratingOldJumpDefault = !settings.Keybindings.ContainsKey("PullRope")
            && settings.Keybindings.TryGetValue("Jump", out string? jumpKey)
            && string.Equals(jumpKey, "Space", StringComparison.OrdinalIgnoreCase);

        GameSettings defaults = new();
        foreach (KeyValuePair<string, string> binding in defaults.Keybindings)
        {
            settings.Keybindings.TryAdd(binding.Key, binding.Value);
        }

        if (migratingOldJumpDefault)
        {
            settings.Keybindings["Jump"] = defaults.Keybindings["Jump"];
            settings.Keybindings["PullRope"] = defaults.Keybindings["PullRope"];
        }

        return settings;
    }

    private static string GetWritablePath()
    {
        return Path.Combine(AppContext.BaseDirectory, "Content", SettingsFileName);
    }

    private static IEnumerable<string> GetReadablePaths()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "Content", SettingsFileName);
        yield return Path.Combine(AppContext.BaseDirectory, SettingsFileName);
        yield return Path.Combine(Environment.CurrentDirectory, "Content", SettingsFileName);
        yield return Path.Combine(Environment.CurrentDirectory, SettingsFileName);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }
}
