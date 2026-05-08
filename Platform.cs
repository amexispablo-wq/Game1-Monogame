using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game1_Monogame;

public sealed class Platform
{
    public Platform(Rectangle bounds, GameColor color)
    {
        Bounds = bounds;
        PlatformColor = color;
    }

    public Rectangle Bounds { get; set; }
    public GameColor PlatformColor { get; set; }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, bool debugDraw)
    {
        spriteBatch.Draw(pixel, Bounds, PlatformColor.ToXnaColor());
        DrawHelper.DrawBorder(spriteBatch, pixel, Bounds, Color.Black, 2);

        if (debugDraw)
        {
            DrawHelper.DrawBorder(spriteBatch, pixel, Bounds, Color.White, 1);
        }
    }
}
