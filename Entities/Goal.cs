using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public sealed class Goal
{
    public static readonly Point FixedSize = new(48, 64);

    public Goal(Point position)
    {
        Position = position;
    }

    public Point Position { get; set; }
    public Rectangle Bounds => new(Position.X, Position.Y, FixedSize.X, FixedSize.Y);
    public Rectangle TriggerBounds => Bounds;

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, bool debugDraw, float alpha = 1f)
    {
        DrawFlag(spriteBatch, pixel, Bounds, alpha);

        if (debugDraw)
        {
            DrawHelper.DrawBorder(spriteBatch, pixel, TriggerBounds, Color.White * alpha, 1);
        }
    }

    public static void DrawIcon(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, float alpha = 1f)
    {
        DrawFlag(spriteBatch, pixel, bounds, alpha);
    }

    private static void DrawFlag(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, float alpha)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        alpha = MathHelper.Clamp(alpha, 0f, 1f);

        int poleWidth = Math.Max(3, bounds.Width / 10);
        int poleX = bounds.Left + Math.Max(4, bounds.Width / 4);
        Rectangle pole = new(poleX, bounds.Top, poleWidth, bounds.Height);

        Color poleColor = new Color(232, 235, 242) * alpha;
        Color poleShadow = new Color(94, 105, 125) * alpha;
        Color flagColor = new Color(255, 207, 72) * alpha;
        Color flagShadow = new Color(209, 79, 66) * alpha;
        Color borderColor = Color.Black * alpha;

        spriteBatch.Draw(pixel, pole, poleColor);
        spriteBatch.Draw(pixel, new Rectangle(pole.Right - 1, pole.Top, 1, pole.Height), poleShadow);

        int flagLeft = pole.Right;
        int flagTop = bounds.Top + Math.Max(2, bounds.Height / 12);
        int flagWidth = Math.Max(8, bounds.Right - flagLeft - Math.Max(2, bounds.Width / 12));
        int flagHeight = Math.Max(10, bounds.Height / 3);
        DrawRightTriangle(spriteBatch, pixel, new Rectangle(flagLeft, flagTop, flagWidth, flagHeight), flagColor);

        int shadowHeight = Math.Max(2, flagHeight / 4);
        DrawRightTriangle(
            spriteBatch,
            pixel,
            new Rectangle(flagLeft, flagTop + flagHeight - shadowHeight, flagWidth, shadowHeight),
            flagShadow);

        Rectangle baseBounds = new(
            bounds.Left + Math.Max(2, bounds.Width / 10),
            bounds.Bottom - Math.Max(5, bounds.Height / 10),
            Math.Max(12, bounds.Width / 2),
            Math.Max(4, bounds.Height / 12));
        spriteBatch.Draw(pixel, baseBounds, poleShadow);

        DrawHelper.DrawBorder(spriteBatch, pixel, pole, borderColor, Math.Max(1, bounds.Width / 48));
        DrawHelper.DrawBorder(spriteBatch, pixel, baseBounds, borderColor, Math.Max(1, bounds.Width / 48));
    }

    private static void DrawRightTriangle(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, Color color)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        for (int y = 0; y < bounds.Height; y++)
        {
            float t = bounds.Height <= 1 ? 0f : y / (float)(bounds.Height - 1);
            int rowWidth = Math.Max(1, (int)MathF.Round(MathHelper.Lerp(bounds.Width, 1, t)));
            spriteBatch.Draw(pixel, new Rectangle(bounds.Left, bounds.Top + y, rowWidth, 1), color);
        }
    }
}
