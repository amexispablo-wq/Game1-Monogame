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

    public static Vector2 ClosestPointOnRectanglePerimeter(Rectangle rect, Vector2 point)
    {
        bool insideX = point.X >= rect.Left && point.X <= rect.Right;
        bool insideY = point.Y >= rect.Top && point.Y <= rect.Bottom;
        if (insideX && insideY)
        {
            float toTop = point.Y - rect.Top;
            float toBottom = rect.Bottom - point.Y;
            float toLeft = point.X - rect.Left;
            float toRight = rect.Right - point.X;
            float min = MathF.Min(MathF.Min(toTop, toBottom), MathF.Min(toLeft, toRight));

            // Nearest face only — no bottom/right bias. Bias caused mid-air V and side loops.
            if (min == toTop)
            {
                return new Vector2(point.X, rect.Top);
            }

            if (min == toBottom)
            {
                return new Vector2(point.X, rect.Bottom);
            }

            if (min == toLeft)
            {
                return new Vector2(rect.Left, point.Y);
            }

            return new Vector2(rect.Right, point.Y);
        }

        if (insideX)
        {
            return new Vector2(point.X, point.Y < rect.Top ? rect.Top : rect.Bottom);
        }

        if (insideY)
        {
            return new Vector2(point.X < rect.Left ? rect.Left : rect.Right, point.Y);
        }

        float cornerX = point.X < rect.Left ? rect.Left : rect.Right;
        float cornerY = point.Y < rect.Top ? rect.Top : rect.Bottom;
        return new Vector2(cornerX, cornerY);
    }

    public static bool TrySnapCircleToRectanglePerimeter(
        ref Vector2 center,
        float radius,
        Rectangle rect,
        Vector2 biasDirection,
        out Vector2 outwardNormal)
    {
        outwardNormal = Vector2.Zero;
        Vector2 probe = center - new Vector2(radius);
        Vector2 size = new(radius * 2f);
        if (!Intersects(probe, size, rect))
        {
            return false;
        }

        Vector2 closest = ClosestPointOnRectanglePerimeter(rect, center);

        // Near-tie faces: prefer gravity bias (usually down → top surface for resting rope).
        if (IsPointStrictlyInside(center, rect))
        {
            closest = PreferBiasedFace(rect, center, biasDirection, closest);
        }

        outwardNormal = ResolveRectangleOutwardNormal(closest, rect, center, biasDirection);
        center = closest + (outwardNormal * radius);
        return true;
    }

    private static bool IsPointStrictlyInside(Vector2 point, Rectangle rect)
    {
        return point.X > rect.Left
            && point.X < rect.Right
            && point.Y > rect.Top
            && point.Y < rect.Bottom;
    }

    private static Vector2 PreferBiasedFace(
        Rectangle rect,
        Vector2 point,
        Vector2 biasDirection,
        Vector2 currentClosest)
    {
        if (biasDirection.LengthSquared() <= 0.0001f)
        {
            return currentClosest;
        }

        Vector2 bias = Vector2.Normalize(biasDirection);
        float toTop = point.Y - rect.Top;
        float toBottom = rect.Bottom - point.Y;
        float toLeft = point.X - rect.Left;
        float toRight = rect.Right - point.X;
        float min = MathF.Min(MathF.Min(toTop, toBottom), MathF.Min(toLeft, toRight));
        const float tieEpsilon = 2.5f;

        // Gravity down → prefer top face when nearly tied (rope rests on platforms).
        if (bias.Y > 0.5f && toTop <= min + tieEpsilon)
        {
            return new Vector2(point.X, rect.Top);
        }

        if (bias.Y < -0.5f && toBottom <= min + tieEpsilon)
        {
            return new Vector2(point.X, rect.Bottom);
        }

        if (bias.X > 0.5f && toLeft <= min + tieEpsilon)
        {
            return new Vector2(rect.Left, point.Y);
        }

        if (bias.X < -0.5f && toRight <= min + tieEpsilon)
        {
            return new Vector2(rect.Right, point.Y);
        }

        return currentClosest;
    }

    private static Vector2 ResolveRectangleOutwardNormal(
        Vector2 closest,
        Rectangle rect,
        Vector2 center,
        Vector2 biasDirection)
    {
        if (MathF.Abs(closest.Y - rect.Top) < 0.01f)
        {
            return new Vector2(0f, -1f);
        }

        if (MathF.Abs(closest.Y - rect.Bottom) < 0.01f)
        {
            return new Vector2(0f, 1f);
        }

        if (MathF.Abs(closest.X - rect.Left) < 0.01f)
        {
            return new Vector2(-1f, 0f);
        }

        if (MathF.Abs(closest.X - rect.Right) < 0.01f)
        {
            return new Vector2(1f, 0f);
        }

        Vector2 fallback = center - closest;
        if (fallback.LengthSquared() <= 0.0001f)
        {
            fallback = biasDirection;
        }

        if (fallback.LengthSquared() <= 0.0001f)
        {
            return new Vector2(0f, 1f);
        }

        return Vector2.Normalize(fallback);
    }

    public static int GetSegmentCollisionSampleCount(float segmentLength, float padding)
    {
        if (segmentLength <= padding * 2f)
        {
            return 7;
        }

        int count = (int)MathF.Ceiling(segmentLength / (padding * 1.5f)) + 1;
        return Math.Clamp(count, 7, 18);
    }

    public static bool SegmentPenetratesPlatformInterior(Vector2 a, Vector2 b, Rectangle rect, float padding)
    {
        if (!SegmentIntersectsExpandedRectangle(a, b, rect, padding))
        {
            return false;
        }

        float segmentLength = Vector2.Distance(a, b);
        int sampleCount = GetSegmentCollisionSampleCount(segmentLength, padding);
        for (int sample = 1; sample < sampleCount; sample++)
        {
            float t = sample / (float)sampleCount;
            Vector2 point = Vector2.Lerp(a, b, t);
            if (IsPointInsideSolid(point, rect, MathF.Max(1f, padding * 0.25f)))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsPointInsideSolid(Vector2 point, Rectangle rect, float padding)
    {
        float left = rect.Left + padding;
        float right = rect.Right - padding;
        float top = rect.Top + padding;
        float bottom = rect.Bottom - padding;
        if (right <= left || bottom <= top)
        {
            return false;
        }

        return point.X > left && point.X < right && point.Y > top && point.Y < bottom;
    }

    public static bool SegmentIntersectsExpandedRectangle(Vector2 a, Vector2 b, Rectangle rect, float padding)
    {
        float left = rect.Left - padding;
        float right = rect.Right + padding;
        float top = rect.Top - padding;
        float bottom = rect.Bottom + padding;

        if (a.X >= left && a.X <= right && a.Y >= top && a.Y <= bottom)
        {
            return true;
        }

        if (b.X >= left && b.X <= right && b.Y >= top && b.Y <= bottom)
        {
            return true;
        }

        Vector2 topLeft = new(left, top);
        Vector2 topRight = new(right, top);
        Vector2 bottomLeft = new(left, bottom);
        Vector2 bottomRight = new(right, bottom);

        return SegmentsIntersect(a, b, topLeft, topRight)
            || SegmentsIntersect(a, b, topRight, bottomRight)
            || SegmentsIntersect(a, b, bottomRight, bottomLeft)
            || SegmentsIntersect(a, b, bottomLeft, topLeft);
    }

    private static bool SegmentsIntersect(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
    {
        Vector2 d1 = a2 - a1;
        Vector2 d2 = b2 - b1;
        float cross = (d1.X * d2.Y) - (d1.Y * d2.X);
        if (MathF.Abs(cross) < 1e-6f)
        {
            return false;
        }

        Vector2 delta = b1 - a1;
        float t = ((delta.X * d2.Y) - (delta.Y * d2.X)) / cross;
        float u = ((delta.X * d1.Y) - (delta.Y * d1.X)) / cross;
        return t >= 0f && t <= 1f && u >= 0f && u <= 1f;
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
