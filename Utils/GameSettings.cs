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

    [JsonPropertyName("musicVolume")]
    public float MusicVolume { get; set; } = 0.75f;

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
        { "Blue", "K" },
        { "Green", "L" },
        { "PullRope", "Space" }
    };

    // Optional per-action gamepad button overrides. Empty = use GamepadDefaults.
    // Only button-style actions are rebindable (Jump, Respawn, Red, Blue, Green).
    [JsonPropertyName("GamepadBindings")]
    public Dictionary<string, string> GamepadBindings { get; set; } = new();

    [JsonPropertyName("ColorMode")]
    public ColorMode ColorMode { get; set; } = ColorMode.Normal;

    [JsonPropertyName("SoundEffects")]
    public Dictionary<string, bool> SoundEffects { get; set; } = CreateDefaultSoundEffects();

    public static Dictionary<string, bool> CreateDefaultSoundEffects()
    {
        return new Dictionary<string, bool>
        {
            { "Jump", true },
            { "PullRope", true },
            { "Red", true },
            { "Blue", true },
            { "Green", true },
            { "Checkpoint", true },
            { "MenuNavigation", true },
            { "Lava", true }
        };
    }
}
