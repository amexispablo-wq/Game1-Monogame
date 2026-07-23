#nullable enable
namespace ColorBlocks;

/// <summary>
/// Which input backend is currently driving local play.
/// Resolved by InputManager — gameplay never sees Steam APIs.
/// </summary>
public enum ActiveInputBackend
{
    Keyboard,
    Gamepad,
    SteamInput
}
