#nullable enable
namespace ColorBlocks;

/// <summary>
/// Steam Input action names. Must match steam_input_manifest.vdf.
/// Gameplay-only — menus synthesize from these in InputManager.
/// </summary>
public static class SteamInputActionNames
{
    public const string ActionSetGameplay = "Gameplay";

    public const string Jump = "Jump";
    public const string PullRope = "PullRope";
    public const string Respawn = "Respawn";
    public const string Pause = "Pause";
    public const string ColorRed = "ColorRed";
    public const string ColorGreen = "ColorGreen";
    public const string ColorBlue = "ColorBlue";
    public const string Move = "Move";

    public static readonly string[] DigitalActions =
    {
        Jump, PullRope, Respawn, Pause,
        ColorRed, ColorGreen, ColorBlue
    };

    public static readonly string[] AnalogActions =
    {
        Move
    };
}
