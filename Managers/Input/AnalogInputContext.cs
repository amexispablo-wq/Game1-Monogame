#nullable enable
namespace ColorBlocks;

/// <summary>
/// Routes the single Steam <c>Move</c> analog vector to gameplay or UI navigation.
/// Set centrally from the active scene — never per-menu.
/// </summary>
public enum AnalogInputContext
{
    /// <summary>Move vector drives player movement; menu stick from Steam is zeroed.</summary>
    Gameplay,
    /// <summary>Move vector drives menu navigation; gameplay move from Steam is zeroed.</summary>
    Menu
}
