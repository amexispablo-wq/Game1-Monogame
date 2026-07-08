#nullable enable
using System;

namespace ColorBlocks;

public static class LevelSourceVisuals
{
    public static string GetIcon(LevelSource source) =>
        source switch
        {
            LevelSource.Official => "O",
            LevelSource.Local => "L",
            LevelSource.Workshop => "W",
            _ => "?"
        };

    public static string GetTabLabel(LevelSource source) =>
        source switch
        {
            LevelSource.Official => "OFFICIAL",
            LevelSource.Local => "MY LEVELS",
            LevelSource.Workshop => "WORKSHOP",
            _ => source.ToString().ToUpperInvariant()
        };

    public static Microsoft.Xna.Framework.Color GetBadgeColor(LevelSource source) =>
        source switch
        {
            LevelSource.Official => new Microsoft.Xna.Framework.Color(120, 170, 230),
            LevelSource.Local => new Microsoft.Xna.Framework.Color(140, 190, 140),
            LevelSource.Workshop => new Microsoft.Xna.Framework.Color(200, 160, 110),
            _ => new Microsoft.Xna.Framework.Color(150, 150, 150)
        };
}
