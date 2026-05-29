namespace ColorBlocks;

public enum RopeGameplayMode
{
    ColoredPhysics,
    Neutral
}

public static class RopeGameplayModeExtensions
{
    public static string ToDisplayName(this RopeGameplayMode mode)
    {
        return mode switch
        {
            RopeGameplayMode.ColoredPhysics => "Colored Physics",
            RopeGameplayMode.Neutral => "Neutral Rope",
            _ => "Unknown"
        };
    }

    public static string ToDebugName(this RopeGameplayMode mode)
    {
        return mode switch
        {
            RopeGameplayMode.ColoredPhysics => "Colored",
            RopeGameplayMode.Neutral => "Neutral",
            _ => "Unknown"
        };
    }

    public static string ToDescription(this RopeGameplayMode mode)
    {
        return mode switch
        {
            RopeGameplayMode.ColoredPhysics => "Rope collides with colored platforms",
            RopeGameplayMode.Neutral => "Rope ignores platforms and stays beige",
            _ => string.Empty
        };
    }
}
