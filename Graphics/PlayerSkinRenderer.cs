using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public static class PlayerSkinRenderer
{
    public static void DrawBody(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bodyBounds,
        Color gameplayColor,
        PlayerSkinData? cosmeticSkin)
    {
        spriteBatch.Draw(pixel, bodyBounds, gameplayColor);

        if (cosmeticSkin is not null)
        {
            DrawSkinOverlay(spriteBatch, pixel, bodyBounds, cosmeticSkin);
        }

        DrawHelper.DrawBorder(spriteBatch, pixel, bodyBounds, Color.Black, 3);
    }

    public static void DrawSkinOverlay(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bodyBounds,
        PlayerSkinData skin)
    {
        int grid = PlayerSkinData.GridSize;
        float cellW = bodyBounds.Width / (float)grid;
        float cellH = bodyBounds.Height / (float)grid;

        for (int y = 0; y < grid; y++)
        {
            for (int x = 0; x < grid; x++)
            {
                if (!skin.GetPixel(x, y))
                {
                    continue;
                }

                int px = (int)MathF.Round(bodyBounds.X + (x * cellW));
                int py = (int)MathF.Round(bodyBounds.Y + (y * cellH));
                int pw = Math.Max(1, (int)MathF.Ceiling(cellW));
                int ph = Math.Max(1, (int)MathF.Ceiling(cellH));
                spriteBatch.Draw(pixel, new Rectangle(px, py, pw, ph), Color.Black);
            }
        }
    }
}
