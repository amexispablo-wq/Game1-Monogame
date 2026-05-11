using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game1_Monogame;

public sealed class Camera
{
    public Camera(Vector2 position)
    {
        Position = position;
    }

    public Vector2 Position { get; set; }
    public float Zoom { get; private set; } = 1f;
    public float MinZoom { get; set; } = 0.25f;
    public float MaxZoom { get; set; } = 4f;

    public Matrix GetTransform(Viewport viewport)
    {
        return Matrix.CreateTranslation(new Vector3(-Position, 0f))
            * Matrix.CreateScale(Zoom, Zoom, 1f)
            * Matrix.CreateTranslation(new Vector3(viewport.Width * 0.5f, viewport.Height * 0.5f, 0f));
    }

    public void SetZoom(float zoom)
    {
        Zoom = MathHelper.Clamp(zoom, MinZoom, MaxZoom);
    }

    public Vector2 ScreenToWorld(Point screenPosition, Viewport viewport)
    {
        return ScreenToWorld(new Vector2(screenPosition.X, screenPosition.Y), viewport);
    }

    public Vector2 ScreenToWorld(Vector2 screenPosition, Viewport viewport)
    {
        Vector2 viewportCenter = new(viewport.Width * 0.5f, viewport.Height * 0.5f);
        return ((screenPosition - viewportCenter) / Zoom) + Position;
    }

    public Rectangle GetVisibleWorldRectangle(Viewport viewport, int padding)
    {
        Vector2 topLeft = ScreenToWorld(Vector2.Zero, viewport);
        Vector2 bottomRight = ScreenToWorld(new Vector2(viewport.Width, viewport.Height), viewport);

        int left = (int)MathF.Floor(MathF.Min(topLeft.X, bottomRight.X)) - padding;
        int top = (int)MathF.Floor(MathF.Min(topLeft.Y, bottomRight.Y)) - padding;
        int right = (int)MathF.Ceiling(MathF.Max(topLeft.X, bottomRight.X)) + padding;
        int bottom = (int)MathF.Ceiling(MathF.Max(topLeft.Y, bottomRight.Y)) + padding;

        return new Rectangle(left, top, right - left, bottom - top);
    }

    public void PanByScreenDelta(Point screenDelta)
    {
        Position -= new Vector2(screenDelta.X, screenDelta.Y) / Zoom;
    }

    public void ZoomAt(float zoomFactor, Point screenFocus, Viewport viewport)
    {
        Vector2 worldBeforeZoom = ScreenToWorld(screenFocus, viewport);
        Zoom = MathHelper.Clamp(Zoom * zoomFactor, MinZoom, MaxZoom);
        Vector2 worldAfterZoom = ScreenToWorld(screenFocus, viewport);
        Position += worldBeforeZoom - worldAfterZoom;
    }
}
