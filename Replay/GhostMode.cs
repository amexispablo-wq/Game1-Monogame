#nullable enable

namespace ColorBlocks;

/// <summary>Which ghost overlays to show during gameplay.</summary>
public enum GhostMode
{
    None,
    PersonalBest,
    WorldRecord,
    Both
}

public static class GhostModeExtensions
{
    public static string ToDisplayName(this GhostMode mode) => mode switch
    {
        GhostMode.None => "No Ghost",
        GhostMode.PersonalBest => "Personal Best",
        GhostMode.WorldRecord => "World Record",
        GhostMode.Both => "Both Ghosts",
        _ => mode.ToString()
    };

    public static bool IncludesPersonalBest(this GhostMode mode) =>
        mode is GhostMode.PersonalBest or GhostMode.Both;

    public static bool IncludesWorldRecord(this GhostMode mode) =>
        mode is GhostMode.WorldRecord or GhostMode.Both;
}
