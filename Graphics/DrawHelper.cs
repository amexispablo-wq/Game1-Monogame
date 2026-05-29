using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public static class DrawHelper
{
    public static void DrawBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rectangle, Color color, int thickness)
    {
        if (rectangle.Width <= 0 || rectangle.Height <= 0 || thickness <= 0)
        {
            return;
        }

        int safeThickness = Math.Min(thickness, Math.Min(rectangle.Width, rectangle.Height));

        spriteBatch.Draw(pixel, new Rectangle(rectangle.Left, rectangle.Top, rectangle.Width, safeThickness), color);
        spriteBatch.Draw(pixel, new Rectangle(rectangle.Left, rectangle.Bottom - safeThickness, rectangle.Width, safeThickness), color);
        spriteBatch.Draw(pixel, new Rectangle(rectangle.Left, rectangle.Top, safeThickness, rectangle.Height), color);
        spriteBatch.Draw(pixel, new Rectangle(rectangle.Right - safeThickness, rectangle.Top, safeThickness, rectangle.Height), color);
    }
}
