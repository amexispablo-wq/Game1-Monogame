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
        DrawBody(
            spriteBatch,
            pixel,
            new Vector2(bodyBounds.Center.X, bodyBounds.Center.Y),
            bodyBounds.Width,
            bodyBounds.Height,
            gameplayColor,
            cosmeticSkin,
            rotation);
    }

    public static void DrawBody(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Vector2 bodyCenter,
        float bodyWidth,
        float bodyHeight,
        Color gameplayColor,
        PlayerSkinData? cosmeticSkin,
        float rotation = 0f)
    {
        DrawRotatedRect(
            spriteBatch,
            pixel,
            bodyCenter,
            bodyWidth,
            bodyHeight,
            rotation,
            gameplayColor);

        Rectangle borderBounds = new(
            (int)MathF.Round(bodyCenter.X - (bodyWidth * 0.5f)),
            (int)MathF.Round(bodyCenter.Y - (bodyHeight * 0.5f)),
            Math.Max(1, (int)MathF.Round(bodyWidth)),
            Math.Max(1, (int)MathF.Round(bodyHeight)));

        if (cosmeticSkin is not null)
        {
            DrawSkinOverlay(spriteBatch, pixel, borderBounds, bodyCenter, cosmeticSkin, rotation);
        }

        DrawHelper.DrawBorder(spriteBatch, pixel, borderBounds, Color.Black, 3, rotation);
    }

    public static void DrawSkinOverlay(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bodyBounds,
        PlayerSkinData skin,
        float rotation = 0f)
    {
        DrawSkinOverlay(
            spriteBatch,
            pixel,
            bodyBounds,
            new Vector2(bodyBounds.Center.X, bodyBounds.Center.Y),
            skin,
            rotation);
    }

    public static void DrawSkinOverlay(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bodyBounds,
        Vector2 center,
        PlayerSkinData skin,
        float rotation = 0f)
    {
        int grid = PlayerSkinData.GridSize;
        float cellW = bodyBounds.Width / (float)grid;
        float cellH = bodyBounds.Height / (float)grid;
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
