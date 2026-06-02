using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public sealed class CheckpointFlag
{
    private static readonly Color InactiveFlagColor = new(255, 160, 48);
    private static readonly Color InactiveFlagShadow = new(190, 88, 38);
    private static readonly Color ActiveFlagColor = new(255, 235, 84);
    private static readonly Color ActiveFlagShadow = new(255, 156, 48);

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
        Color flagColor = IsActive ? ActiveFlagColor : InactiveFlagColor;
        Color flagShadow = IsActive ? ActiveFlagShadow : InactiveFlagShadow;
        Goal.DrawFlag(spriteBatch, pixel, Bounds, flagColor, flagShadow, alpha, IsActive);

        if (IsActive)
        {
            Rectangle marker = new(Bounds.Center.X - 5, Bounds.Top - 8, 10, 10);
            spriteBatch.Draw(pixel, marker, ActiveFlagColor * (0.85f * alpha));
            DrawHelper.DrawBorder(spriteBatch, pixel, marker, Color.Black * alpha, 1);
        }

        if (debugDraw)
        {
            DrawHelper.DrawBorder(spriteBatch, pixel, TriggerBounds, Color.White * alpha, 1);
        }
    }

    public static void DrawIcon(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, float alpha = 1f)
    {
        Goal.DrawFlag(spriteBatch, pixel, bounds, InactiveFlagColor, InactiveFlagShadow, alpha);
    }
}
