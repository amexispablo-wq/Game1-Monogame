#nullable enable
namespace ColorBlocks;

/// <summary>
/// Steam Input digital/analog action names. Must match steam_input_manifest.vdf.
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
    public const string MenuAccept = "MenuAccept";
    public const string MenuCancel = "MenuCancel";
    public const string MenuBack = "MenuBack";
    public const string MenuStart = "MenuStart";

    public const string Move = "Move";
    public const string MenuNavigate = "MenuNavigate";

    public static readonly string[] DigitalActions =
    {
        Jump, PullRope, Respawn, Pause,
        ColorRed, ColorGreen, ColorBlue,
        MenuAccept, MenuCancel, MenuBack, MenuStart
    };

    public static readonly string[] AnalogActions =
    {
        Move, MenuNavigate
    };
}
