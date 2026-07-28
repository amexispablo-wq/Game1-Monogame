using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public static class PlayerSkinRenderer
{
    private static readonly Vector2 PixelOrigin = new(0.5f, 0.5f);

    public static void DrawBody(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bodyBounds,
        Color gameplayColor,
        PlayerSkinData? cosmeticSkin,
        float rotation = 0f)
    {
        Vector2 center = new(bodyBounds.Center.X, bodyBounds.Center.Y);

        DrawRotatedRect(
            spriteBatch,
            pixel,
            center,
            bodyBounds.Width,
            bodyBounds.Height,
            rotation,
            gameplayColor);

        if (cosmeticSkin is not null)
        {
            DrawSkinOverlay(spriteBatch, pixel, bodyBounds, cosmeticSkin, rotation);
        }

        DrawHelper.DrawBorder(spriteBatch, pixel, bodyBounds, Color.Black, 3, rotation);
    }

    public static void DrawSkinOverlay(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bodyBounds,
        PlayerSkinData skin,
        float rotation = 0f)
    {
        int grid = PlayerSkinData.GridSize;
        float cellW = bodyBounds.Width / (float)grid;
        float cellH = bodyBounds.Height / (float)grid;
        Vector2 center = new(bodyBounds.Center.X, bodyBounds.Center.Y);
        float cos = MathF.Cos(rotation);
        float sin = MathF.Sin(rotation);

        for (int y = 0; y < grid; y++)
        {
            for (int x = 0; x < grid; x++)
            {
                if (!skin.GetPixel(x, y))
                {
                    continue;
                }

                float localX = bodyBounds.X + ((x + 0.5f) * cellW) - center.X;
                float localY = bodyBounds.Y + ((y + 0.5f) * cellH) - center.Y;
                float worldX = center.X + (localX * cos) - (localY * sin);
                float worldY = center.Y + (localX * sin) + (localY * cos);

                int pw = Math.Max(1, (int)MathF.Ceiling(cellW));
                int ph = Math.Max(1, (int)MathF.Ceiling(cellH));
                DrawRotatedRect(spriteBatch, pixel, new Vector2(worldX, worldY), pw, ph, rotation, Color.Black);
            }
        }
    }

    private static void DrawRotatedRect(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Vector2 center,
        float width,
        float height,
        float rotation,
        Color color)
    {
        spriteBatch.Draw(
            pixel,
            center,
            null,
            color,
            rotation,
            PixelOrigin,
            new Vector2(width, height),
            SpriteEffects.None,
            0f);
    }
}
