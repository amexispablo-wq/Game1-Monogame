#nullable enable
namespace ColorBlocks;

/// <summary>
/// High-level controller family for UI glyphs only. Never used for gameplay branching.
/// </summary>
public enum SteamInputControllerType
{
    Unknown,
    Generic,
    Xbox,
    PlayStation,
    Switch,
    SteamDeck,
    SteamController
}
