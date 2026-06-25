using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

/// <summary>
/// A horizontal, infinitely-wide lava surface. Below the surface everything is
/// lethal molten lava. There is at most one per level. It can only be moved
/// vertically in the editor (never resized/copied/pasted/deleted). In Lava Rise
/// mode the surface climbs upward smoothly over time.
/// </summary>
public sealed class LavaLine
{
    public const float DefaultRiseSpeed = 70f;   // px / second
    public const float MinRiseSpeed = 0f;
    public const float MaxRiseSpeed = 400f;
    public const int EditorHitMargin = 22;       // vertical pick tolerance in world px

    private static readonly Color BodyDeep = new(150, 38, 0);
    private static readonly Color BodyMid = new(214, 74, 8);
    private static readonly Color BodyBright = new(255, 122, 24);
    private static readonly Color SurfaceGlow = new(255, 190, 78);
    private static readonly Color ParticleColor = new(255, 158, 52);
    private static readonly Color EditorLineColor = new(255, 138, 28);

    public LavaLine(int surfaceY, float riseSpeed = DefaultRiseSpeed)
    {
        SurfaceY = surfaceY;
        RiseSpeed = MathHelper.Clamp(riseSpeed, MinRiseSpeed, MaxRiseSpeed);
    }

    public int SurfaceY { get; set; }
    public float RiseSpeed { get; set; }

    public bool HitTest(Point worldPoint) => Math.Abs(worldPoint.Y - SurfaceY) <= EditorHitMargin;

    /// <summary>Player dies when any part of its body sinks below the lava surface.</summary>
    public static bool IsLethal(Rectangle playerBounds, float surfaceY) => playerBounds.Bottom > surfaceY;

    /// <summary>
    /// Draws the molten body + animated wavy surface + decorative (non-lethal)
    /// splash particles, clipped to the visible world rectangle so an effectively
    /// infinite lava field stays cheap to render.
    /// </summary>
    public static void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle view,
        float surfaceY,
        float seconds,
        bool drawParticles)
    {
        if (view.Width <= 0 || view.Height <= 0 || surfaceY >= view.Bottom)
        {
            return;
        }

        int left = view.Left;
        int right = view.Right;
        int bodyTop = Math.Max((int)MathF.Floor(surfaceY), view.Top);
        int bodyBottom = view.Bottom;
        int bodyHeight = bodyBottom - bodyTop;

        if (bodyHeight > 0)
        {
            spriteBatch.Draw(pixel, new Rectangle(left, bodyTop, right - left, bodyHeight), BodyMid);

            const int cell = 48;
            int startCx = (int)MathF.Floor(left / (float)cell);
            int endCx = (int)MathF.Ceiling(right / (float)cell);
            int startCy = (int)MathF.Floor(bodyTop / (float)cell);
            int endCy = (int)MathF.Ceiling(bodyBottom / (float)cell);

            for (int cy = startCy; cy < endCy; cy++)
            {
                for (int cx = startCx; cx < endCx; cx++)
                {
                    float n = Hash01(cx, cy);
                    float pulse = 0.5f + 0.5f * MathF.Sin((seconds * 1.6f) + (n * MathHelper.TwoPi) + cy * 0.6f);
                    float t = MathHelper.Clamp(n * 0.6f + pulse * 0.4f, 0f, 1f);
                    Color tone = t < 0.5f
                        ? Color.Lerp(BodyDeep, BodyMid, t * 2f)
                        : Color.Lerp(BodyMid, BodyBright, (t - 0.5f) * 2f);

                    int rl = Math.Max(cx * cell, left);
                    int rt = Math.Max(cy * cell, bodyTop);
                    int rr = Math.Min((cx * cell) + cell, right);
                    int rb = Math.Min((cy * cell) + cell, bodyBottom);
                    if (rr > rl && rb > rt)
                    {
                        spriteBatch.Draw(pixel, new Rectangle(rl, rt, rr - rl, rb - rt), tone * 0.5f);
                    }
                }
            }
        }

        // Animated surface band with a wavy top edge.
        const int step = 8;
        for (int x = left; x < right; x += step)
        {
            float wave = MathF.Sin((x * 0.025f) + seconds * 3.2f) * 5f
                       + MathF.Sin((x * 0.011f) - seconds * 1.7f) * 4f;
            int top = (int)MathF.Round(surfaceY + wave);
            int w = Math.Min(step, right - x);
            spriteBatch.Draw(pixel, new Rectangle(x, top, w, 6), SurfaceGlow);
            spriteBatch.Draw(pixel, new Rectangle(x, top + 6, w, 10), BodyBright * 0.8f);
        }

        if (!drawParticles)
        {
            return;
        }

        // Decorative splashes above the surface. Purely visual: they are never lethal.
        const int particleCount = 28;
        int span = Math.Max(1, right - left);
        for (int i = 0; i < particleCount; i++)
        {
            float seed = Hash01((i * 7) + 3, (i * 13) + 5);
            float cycle = ((seconds * 0.6f) + seed) % 1f;
            float px = left + (((seed * 9301f) + (i * 131f)) % span);
            float wave = MathF.Sin((px * 0.025f) + seconds * 3.2f) * 5f;
            float baseY = surfaceY + wave;
            float arc = MathF.Sin(cycle * MathF.PI);
            float height = 26f + (seed * 34f);
            float py = baseY - (arc * height);
            int size = Math.Max(2, (int)MathF.Round(3f + ((1f - cycle) * 3f)));
            spriteBatch.Draw(pixel, new Rectangle((int)px, (int)py, size, size), ParticleColor * MathHelper.Clamp(arc, 0f, 1f));
        }
    }

    /// <summary>Editor preview: full-width surface line + faint body so the designer sees the lava plane.</summary>
    public static void DrawEditorLine(SpriteBatch spriteBatch, Texture2D pixel, Rectangle view, int surfaceY)
    {
        int left = view.Left;
        int width = view.Width;
        spriteBatch.Draw(pixel, new Rectangle(left, surfaceY, width, Math.Max(0, view.Bottom - surfaceY)), BodyMid * 0.28f);
        spriteBatch.Draw(pixel, new Rectangle(left, surfaceY - 2, width, 6), EditorLineColor);
        spriteBatch.Draw(pixel, new Rectangle(left, surfaceY + 4, width, 10), BodyBright * 0.55f);
    }

    private static float Hash01(int x, int y)
    {
        unchecked
        {
            int h = (x * 374761393) + (y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0x7fffffff) / (float)int.MaxValue;
        }
    }
}
