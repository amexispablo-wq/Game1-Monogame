#nullable enable
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ColorBlocks;

public sealed class GameSettings
{
    [JsonPropertyName("displayMode")]
    public string DisplayMode { get; set; } = "Borderless";

    [JsonPropertyName("resolutionWidth")]
    public int ResolutionWidth { get; set; } = 1920;

    [JsonPropertyName("resolutionHeight")]
    public int ResolutionHeight { get; set; } = 1080;

    public const int CurrentAudioMixVersion = 2;

    [JsonPropertyName("musicVolume")]
    public float MusicVolume { get; set; } = 0.5f;

    /// <summary>
    /// Bumped when shipped audio files are recalibrated. Missing/older values reset
    /// music and SFX sliders to 0.5 so saved mix values match the new files.
    /// </summary>
    [JsonPropertyName("audioMixVersion")]
    public int AudioMixVersion { get; set; }

    /// <summary>When true, menu playlist keeps playing during levels instead of level/silence tracks.</summary>
    [JsonPropertyName("continueMenuMusicInLevels")]
    public bool ContinueMenuMusicInLevels { get; set; }

    /// <summary>When true, the in-level controls HUD lists current key/button bindings.</summary>
    [JsonPropertyName("showControlsHud")]
    public bool ShowControlsHud { get; set; } = true;

    /// <summary>When true, a 3-second spawn-hold countdown runs on level start / restart / respawn.</summary>
    [JsonPropertyName("showSpawnCountdown")]
    public bool ShowSpawnCountdown { get; set; } = true;

    // FPS cap. -1 = VSync (monitor refresh), 0 = Unlimited, >0 = hard cap.
    [JsonPropertyName("fpsLimit")]
    public int FpsLimit { get; set; } = 0;

    [JsonPropertyName("Keybindings")]
    public Dictionary<string, string> Keybindings { get; set; } = new()
    {
        { "MoveLeft", "A" },
        { "MoveRight", "D" },
        { "Jump", "W" },
        { "Respawn", "R" },
        { "FastFall", "S" },
        { "Red", "J" },
        { "Blue", "L" },
        { "Green", "K" },
        { "PullRope", "Space" },
        { "RestartLevel", "F" }
    };

    // Optional per-action gamepad button overrides. Empty = use GamepadDefaults.
    // Button-style rebindable: Jump, Respawn, RestartLevel, Red, Blue, Green, etc.
    [JsonPropertyName("GamepadBindings")]
    public Dictionary<string, string> GamepadBindings { get; set; } = new();

    [JsonPropertyName("ColorMode")]
    public ColorMode ColorMode { get; set; } = ColorMode.Normal;

    [JsonPropertyName("SoundEffects")]
    [JsonConverter(typeof(SoundEffectVolumeDictionaryConverter))]
    public Dictionary<string, float> SoundEffects { get; set; } = CreateDefaultSoundEffects();

    public static Dictionary<string, float> CreateDefaultSoundEffects()
    {
        return new Dictionary<string, float>
        {
            { "Jump", 0.5f },
            { "PullRope", 0.5f },
            { "Red", 0.5f },
            { "Blue", 0.5f },
            { "Green", 0.5f },
            { "Checkpoint", 0.5f },
            { "PhysicsExpulsion", 0.5f },
            { "LaunchPad", 0.5f },
            { "MenuNavigation", 0.5f },
            { "Lava", 0.5f }
        };
    }
}
