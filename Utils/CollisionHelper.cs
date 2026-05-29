using System;
using Microsoft.Xna.Framework;

namespace ColorBlocks;

public static class CollisionHelper
{
    public static Rectangle ToRectangle(Vector2 position, Vector2 size)
    {
        return new Rectangle(
            (int)MathF.Round(position.X),
            (int)MathF.Round(position.Y),
            (int)MathF.Round(size.X),
            (int)MathF.Round(size.Y));
    }

    public static bool Intersects(Vector2 position, Vector2 size, Rectangle obstacle)
    {
        float left = position.X;
        float right = position.X + size.X;
        float top = position.Y;
        float bottom = position.Y + size.Y;

        return left < obstacle.Right
            && right > obstacle.Left
            && top < obstacle.Bottom
            && bottom > obstacle.Top;
    }

    public static Vector2 GetShortestEscapeVector(Vector2 position, Vector2 size, Rectangle obstacle)
    {
        return TryGetMinimumTranslationVector(position, size, obstacle, out Vector2 escapeVector, out _, out _)
            ? escapeVector
            : Vector2.Zero;
    }

    public static bool TryGetMinimumTranslationVector(
        Vector2 position,
        Vector2 size,
        Rectangle obstacle,
        out Vector2 escapeVector,
        out Vector2 escapeDirection,
        out float penetrationDepth)
    {
        escapeVector = Vector2.Zero;
        escapeDirection = Vector2.Zero;
        penetrationDepth = 0f;

        if (!Intersects(position, size, obstacle))
        {
            return false;
        }

        float left = position.X;
        float right = position.X + size.X;
        float top = position.Y;
        float bottom = position.Y + size.Y;

        float moveLeft = obstacle.Left - right;
        float moveRight = obstacle.Right - left;
        float moveUp = obstacle.Top - bottom;
        float moveDown = obstacle.Bottom - top;

        Vector2 best = new(moveLeft, 0f);
        float bestDistance = MathF.Abs(moveLeft);

        if (MathF.Abs(moveRight) < bestDistance)
        {
            best = new Vector2(moveRight, 0f);
            bestDistance = MathF.Abs(moveRight);
        }

        if (MathF.Abs(moveUp) < bestDistance)
        {
            best = new Vector2(0f, moveUp);
            bestDistance = MathF.Abs(moveUp);
        }

        if (MathF.Abs(moveDown) < bestDistance)
        {
            best = new Vector2(0f, moveDown);
            bestDistance = MathF.Abs(moveDown);
        }

        escapeVector = best;
        penetrationDepth = bestDistance;
        escapeDirection = Vector2.Normalize(best);
        return true;
    }

    public static bool HasGroundBelow(Vector2 position, Vector2 size, Level level, GameColor color)
    {
        Vector2 probePosition = position + new Vector2(0f, 2f);

        foreach (Platform platform in level.GetCollidablePlatforms(color))
        {
            if (Intersects(probePosition, size, platform.Bounds))
            {
                return true;
            }
        }

        return false;
    }
}
