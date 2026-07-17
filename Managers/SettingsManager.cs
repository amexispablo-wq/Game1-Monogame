#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColorBlocks;

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
        _pendingSettings = CloneSettings(_currentSettings);
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
                    ColorPaletteManager.ApplySettings(_currentSettings.ColorMode);
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
            }
        }

        _currentSettings = NormalizeSettings(new GameSettings());
        ColorPaletteManager.ApplySettings(_currentSettings.ColorMode);
    }

    public static void SaveSettings(GameSettings settings)
    {
        _currentSettings = NormalizeSettings(CloneSettings(settings));

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
        _pendingSettings = CloneSettings(_currentSettings);
        ColorPaletteManager.ApplySettings(_currentSettings.ColorMode);
    }

    public static GameSettings CreateSnapshot(GameSettings source) => CloneSettings(source);

    public static void RestorePendingFromSnapshot(GameSettings snapshot)
    {
        _pendingSettings = CloneSettings(snapshot);
    }

    public static float GetMusicVolume() => _currentSettings.MusicVolume;
    public static string GetKeybinding(string actionName) => _currentSettings.Keybindings.TryGetValue(actionName, out var key) ? key : "UNBOUND";

    private static GameSettings NormalizeSettings(GameSettings settings)
    {
        settings.Keybindings ??= new Dictionary<string, string>();
        settings.GamepadBindings ??= new Dictionary<string, string>();
        settings.SoundEffects ??= GameSettings.CreateDefaultSoundEffects();

        foreach (KeyValuePair<string, bool> effect in GameSettings.CreateDefaultSoundEffects())
        {
            settings.SoundEffects.TryAdd(effect.Key, effect.Value);
        }

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

    private static GameSettings CloneSettings(GameSettings source)
    {
        return new GameSettings
        {
            DisplayMode = source.DisplayMode,
            ResolutionWidth = source.ResolutionWidth,
            ResolutionHeight = source.ResolutionHeight,
            MusicVolume = source.MusicVolume,
            FpsLimit = source.FpsLimit,
            Keybindings = new Dictionary<string, string>(source.Keybindings),
            GamepadBindings = new Dictionary<string, string>(source.GamepadBindings),
            ColorMode = source.ColorMode,
            SoundEffects = new Dictionary<string, bool>(source.SoundEffects)
        };
    }

    private static string GetWritablePath()
    {
        return UserDataPaths.SettingsFile;
    }

    private static IEnumerable<string> GetReadablePaths()
    {
        yield return UserDataPaths.SettingsFile;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }
}
