using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public sealed class CheckpointFlag
{
    public static readonly Point FixedSize = Goal.FixedSize;

    public CheckpointFlag(Point position, int id = 0)
    {
        Position = position;
        Id = id;
    }

    public int Id { get; set; }
    public Point Position { get; set; }
    public bool IsActive { get; set; }
    public Rectangle Bounds => new(Position.X, Position.Y, FixedSize.X, FixedSize.Y);
    public Rectangle TriggerBounds => Bounds;
    public Vector2 RespawnPosition => new(Position.X, Position.Y);

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, bool debugDraw, float alpha = 1f)
    {
        Color flagColor = ColorPaletteManager.GetCheckpointColor(IsActive);
        Color flagShadow = ColorPaletteManager.GetCheckpointShadow(IsActive);
        Goal.DrawFlag(spriteBatch, pixel, Bounds, flagColor, flagShadow, alpha, IsActive);

        if (IsActive)
        {
            Rectangle marker = new(Bounds.Center.X - 5, Bounds.Top - 8, 10, 10);
            spriteBatch.Draw(pixel, marker, flagColor * (0.85f * alpha));
            DrawHelper.DrawBorder(spriteBatch, pixel, marker, Color.Black * alpha, 1);
        }

        if (debugDraw)
        {
            DrawHelper.DrawBorder(spriteBatch, pixel, TriggerBounds, Color.White * alpha, 1);
        }
    }

    public static void DrawIcon(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, float alpha = 1f)
    {
        Goal.DrawFlag(
            spriteBatch,
            pixel,
            bounds,
            ColorPaletteManager.GetCheckpointColor(active: false),
            ColorPaletteManager.GetCheckpointShadow(active: false),
            alpha);
    }
}
