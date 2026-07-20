using Microsoft.Xna.Framework;

namespace ColorBlocks;

public enum GameColor
{
    Red,
    Blue,
    Green,
    White
}

public static class GameColorExtensions
{
    public static Color ToXnaColor(this GameColor color) => ColorPaletteManager.GetGameColor(color);
}
