using Microsoft.Xna.Framework;

namespace ColorBlocks;

public enum GameColor
{
    Red,
    Blue,
    Green
}

public static class GameColorExtensions
{
    public static Color ToXnaColor(this GameColor color)
    {
        return color switch
        {
            GameColor.Red => new Color(224, 64, 64),
            GameColor.Blue => new Color(64, 128, 224),
            GameColor.Green => new Color(72, 184, 96),
            _ => Color.White
        };
    }
}
