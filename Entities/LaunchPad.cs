using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public sealed class LaunchPad
{
    public const int DefaultWidth = 96;
    public const int DefaultHeight = 36;

    public static float LaunchPadForce { get; set; } = 980f;
    public static float LaunchPadCooldown { get; set; } = 0.22f;
    public static float LaunchPadParticleRate { get; set; } = 7f;

    public LaunchPad(Rectangle bounds, float rotationDegrees = 0f)
    {
        Bounds = bounds;
        RotationDegrees = rotationDegrees;
    }

    public Point Position
    {
        get => Bounds.Location;
        set => Bounds = new Rectangle(value.X, value.Y, Bounds.Width, Bounds.Height);
    }

    public Rectangle Bounds { get; set; }
    public float RotationDegrees { get; set; }
    public float RotationRadians => MathHelper.ToRadians(NormalizeRotation(RotationDegrees));
    public Rectangle TriggerBounds => Bounds;
    public Vector2 Center => new(Bounds.Center.X, Bounds.Center.Y);

    public Vector2 LaunchDirection
    {
        get
        {
            float radians = RotationRadians;
            Vector2 direction = new(MathF.Sin(radians), -MathF.Cos(radians));
            return direction.LengthSquared() <= 0.0001f
                ? new Vector2(0f, -1f)
                : Vector2.Normalize(direction);
        }
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, bool debugDraw, float animationSeconds = 0f, float alpha = 1f, bool isEditorMode = false)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        alpha = MathHelper.Clamp(alpha, 0f, 1f);
        DrawParticles(spriteBatch, pixel, animationSeconds, alpha);
        DrawPadBody(spriteBatch, pixel, alpha);
        if (isEditorMode)
        {
            DrawDirectionArrow(spriteBatch, pixel, alpha);
        }

        if (debugDraw)
        {
            DrawHelper.DrawBorder(spriteBatch, pixel, TriggerBounds, Color.White * alpha, 1);
        }
    }

    public static void DrawIcon(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, float alpha = 1f)
    {
        LaunchPad icon = new(bounds, 0f);
        icon.Draw(spriteBatch, pixel, debugDraw: false, animationSeconds: 0.25f, alpha, isEditorMode: true);
    }

    public static float NormalizeRotation(float rotationDegrees)
    {
        float result = rotationDegrees % 360f;
        return result < 0f ? result + 360f : result;
    }

    private void DrawPadBody(SpriteBatch spriteBatch, Texture2D pixel, float alpha)
    {
        int width = Math.Max(6, Bounds.Width);
        int height = Math.Max(6, Bounds.Height);
        int rowStep = Math.Max(1, height / 18);
        Color fill = ColorPaletteManager.Get(ColorType.LaunchPad) * alpha;
        Color bright = ColorPaletteManager.GetLaunchPadBright() * alpha;
        Color baseColor = ColorPaletteManager.GetLaunchPadBase() * alpha;
        Color border = ColorPaletteManager.Get(ColorType.Black) * alpha;

        for (int y = 0; y < height; y += rowStep)
        {
            float progress = height <= 1 ? 1f : y / (float)(height - 1);
            float arch = MathF.Sqrt(MathHelper.Clamp(1f - ((1f - progress) * (1f - progress)), 0f, 1f));
            int rowWidth = Math.Max(3, (int)MathF.Round(width * arch));
            float localY = -height * 0.5f + y + (rowStep * 0.5f);
            DrawLocalRect(
                spriteBatch,
                pixel,
                new RectangleF(-rowWidth * 0.5f, localY - (rowStep * 0.5f), rowWidth, rowStep),
                progress < 0.22f ? bright : fill);
        }

        float baseHeight = Math.Max(4f, height * 0.24f);
        DrawLocalRect(
            spriteBatch,
            pixel,
            new RectangleF(-width * 0.5f, height * 0.5f - baseHeight, width, baseHeight),
            baseColor);

        DrawLocalLine(spriteBatch, pixel, new Vector2(-width * 0.5f, height * 0.5f), new Vector2(width * 0.5f, height * 0.5f), border, 3f);
        DrawLocalLine(spriteBatch, pixel, new Vector2(-width * 0.42f, height * 0.18f), new Vector2(width * 0.42f, height * 0.18f), border * 0.8f, 2f);
    }

    private void DrawDirectionArrow(SpriteBatch spriteBatch, Texture2D pixel, float alpha)
    {
        float height = Math.Max(6, Bounds.Height);
        Vector2 start = new(0f, height * 0.24f);
        Vector2 end = new(0f, -height * 0.68f);
        Color arrowColor = ColorPaletteManager.GetLaunchPadArrow() * alpha;

        DrawLocalLine(spriteBatch, pixel, start, end, arrowColor, 4f);
        DrawLocalLine(spriteBatch, pixel, end, end + new Vector2(-Math.Max(8f, Bounds.Width * 0.12f), Math.Max(8f, height * 0.22f)), arrowColor, 4f);
        DrawLocalLine(spriteBatch, pixel, end, end + new Vector2(Math.Max(8f, Bounds.Width * 0.12f), Math.Max(8f, height * 0.22f)), arrowColor, 4f);
    }

    private void DrawParticles(SpriteBatch spriteBatch, Texture2D pixel, float animationSeconds, float alpha)
    {
        Vector2 direction = LaunchDirection;
        Vector2 tangent = new(direction.Y, -direction.X);
        Vector2 surface = Center + (direction * (Bounds.Height * 0.35f));
        int particleCount = Math.Clamp((int)MathF.Round(LaunchPadParticleRate), 3, 12);

        for (int i = 0; i < particleCount; i++)
        {
            float seed = i / (float)particleCount;
            float phase = (animationSeconds * MathF.Max(0.1f, LaunchPadParticleRate * 0.32f) + seed) % 1f;
            float spread = MathF.Sin((phase + seed) * MathF.PI * 2f) * Bounds.Width * 0.22f;
            Vector2 position = surface + (direction * (phase * Bounds.Height * 1.55f)) + (tangent * spread);
            int size = Math.Max(2, (int)MathF.Round(MathHelper.Lerp(5f, 2f, phase)));
            Rectangle particle = new(
                (int)MathF.Round(position.X) - (size / 2),
                (int)MathF.Round(position.Y) - (size / 2),
                size,
                size);

            spriteBatch.Draw(pixel, particle, ColorPaletteManager.GetLaunchPadParticle() * ((1f - phase) * 0.55f * alpha));
        }
    }

    private void DrawLocalRect(SpriteBatch spriteBatch, Texture2D pixel, RectangleF localBounds, Color color)
    {
        Vector2 localCenter = new(localBounds.X + (localBounds.Width * 0.5f), localBounds.Y + (localBounds.Height * 0.5f));
        Vector2 worldCenter = TransformLocal(localCenter);
        spriteBatch.Draw(
            pixel,
            worldCenter,
            null,
            color,
            RotationRadians,
            new Vector2(0.5f, 0.5f),
            new Vector2(Math.Max(1f, localBounds.Width), Math.Max(1f, localBounds.Height)),
            SpriteEffects.None,
            0f);
    }

    private void DrawLocalLine(SpriteBatch spriteBatch, Texture2D pixel, Vector2 localStart, Vector2 localEnd, Color color, float thickness)
    {
        Vector2 start = TransformLocal(localStart);
        Vector2 end = TransformLocal(localEnd);
        Vector2 delta = end - start;
        float length = delta.Length();
        if (length <= 0.01f)
        {
            return;
        }

        float rotation = MathF.Atan2(delta.Y, delta.X);
        spriteBatch.Draw(
            pixel,
            start,
            null,
            color,
            rotation,
            new Vector2(0f, 0.5f),
            new Vector2(length, Math.Max(1f, thickness)),
            SpriteEffects.None,
            0f);
    }

    private Vector2 TransformLocal(Vector2 local)
    {
        float radians = RotationRadians;
        float cos = MathF.Cos(radians);
        float sin = MathF.Sin(radians);
        return Center + new Vector2(
            (local.X * cos) - (local.Y * sin),
            (local.X * sin) + (local.Y * cos));
    }

    private readonly record struct RectangleF(float X, float Y, float Width, float Height);
}
