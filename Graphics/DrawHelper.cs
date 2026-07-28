using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public static class DrawHelper
{
    private static readonly Vector2 PixelOrigin = new(0.5f, 0.5f);

    public static void DrawBorder(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle rectangle,
        Color color,
        int thickness,
        float rotation = 0f)
    {
        if (rectangle.Width <= 0 || rectangle.Height <= 0 || thickness <= 0)
        {
            return;
        }

        int safeThickness = Math.Min(thickness, Math.Min(rectangle.Width, rectangle.Height));
        if (MathF.Abs(rotation) < 0.0001f)
        {
            spriteBatch.Draw(pixel, new Rectangle(rectangle.Left, rectangle.Top, rectangle.Width, safeThickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rectangle.Left, rectangle.Bottom - safeThickness, rectangle.Width, safeThickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rectangle.Left, rectangle.Top, safeThickness, rectangle.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(rectangle.Right - safeThickness, rectangle.Top, safeThickness, rectangle.Height), color);
            return;
        }

        Vector2 center = new(rectangle.Center.X, rectangle.Center.Y);
        float halfW = rectangle.Width * 0.5f;
        float halfH = rectangle.Height * 0.5f;
        float inset = safeThickness * 0.5f;

        DrawRotatedEdge(spriteBatch, pixel, center, 0f, -halfH + inset, rectangle.Width, safeThickness, rotation, color);
        DrawRotatedEdge(spriteBatch, pixel, center, 0f, halfH - inset, rectangle.Width, safeThickness, rotation, color);
        DrawRotatedEdge(spriteBatch, pixel, center, -halfW + inset, 0f, safeThickness, rectangle.Height, rotation, color);
        DrawRotatedEdge(spriteBatch, pixel, center, halfW - inset, 0f, safeThickness, rectangle.Height, rotation, color);
    }

    private static void DrawRotatedEdge(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Vector2 bodyCenter,
        float localX,
        float localY,
        float width,
        float height,
        float rotation,
        Color color)
    {
        float cos = MathF.Cos(rotation);
        float sin = MathF.Sin(rotation);
        Vector2 world = new(
            bodyCenter.X + (localX * cos) - (localY * sin),
            bodyCenter.Y + (localX * sin) + (localY * cos));

        spriteBatch.Draw(
            pixel,
            world,
            null,
            color,
            rotation,
            PixelOrigin,
            new Vector2(width, height),
            SpriteEffects.None,
            0f);
    }
}
