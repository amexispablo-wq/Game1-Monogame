using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public enum PowerUpType : byte
{
    Speed = 0,
    Jump = 1
}

public sealed class PowerUp
{
    public const int DefaultWidth = 32;
    public const int DefaultHeight = 32;
    public const float DefaultDurationSeconds = 5f;
    public const float DefaultMultiplier = 1.5f;
    public const float DefaultRespawnSeconds = 0f;
    public const bool DefaultConsumable = true;
    public const float MinDurationSeconds = 0.05f;
    public const float MaxDurationSeconds = 60f;
    public const float MinMultiplier = 1f;
    public const float MaxMultiplier = 3f;
    public const float DurationStep = 0.5f;
    public const float MultiplierStep = 0.1f;
    public const float RespawnStep = 0.5f;

    public PowerUp(
        Rectangle bounds,
        PowerUpType type = PowerUpType.Speed,
        float durationSeconds = DefaultDurationSeconds,
        float multiplier = DefaultMultiplier,
        float respawnSeconds = DefaultRespawnSeconds,
        bool consumable = DefaultConsumable)
    {
        Bounds = bounds;
        Type = type;
        DurationSeconds = ClampDuration(durationSeconds);
        Multiplier = ClampMultiplier(multiplier);
        RespawnSeconds = ClampRespawn(respawnSeconds);
        Consumable = consumable;
        IsAvailable = true;
    }

    public Point Position
    {
        get => Bounds.Location;
        set => Bounds = new Rectangle(value.X, value.Y, Bounds.Width, Bounds.Height);
    }

    public Rectangle Bounds { get; set; }
    public PowerUpType Type { get; set; }
    public float DurationSeconds { get; set; }
    public float Multiplier { get; set; }
    public float RespawnSeconds { get; set; }
    public bool Consumable { get; set; } = DefaultConsumable;
    public bool IsAvailable { get; private set; }
    public float RespawnRemaining { get; private set; }
    public Rectangle TriggerBounds => Bounds;
    public Vector2 Center => new(Bounds.Center.X, Bounds.Center.Y);

    public void Collect()
    {
        if (!IsAvailable || !Consumable)
        {
            return;
        }

        IsAvailable = false;
        RespawnRemaining = RespawnSeconds > 0f ? RespawnSeconds : 0f;
    }

    public void TickRespawn(float dt)
    {
        if (IsAvailable || RespawnSeconds <= 0f || RespawnRemaining <= 0f)
        {
            return;
        }

        RespawnRemaining = MathF.Max(0f, RespawnRemaining - dt);
        if (RespawnRemaining <= 0f)
        {
            IsAvailable = true;
            RespawnRemaining = 0f;
        }
    }

    public void ResetAvailability()
    {
        IsAvailable = true;
        RespawnRemaining = 0f;
    }

    public void ApplyRuntimeState(bool isAvailable, float respawnRemaining)
    {
        IsAvailable = isAvailable;
        RespawnRemaining = MathF.Max(0f, respawnRemaining);
        if (IsAvailable)
        {
            RespawnRemaining = 0f;
        }
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, bool debugDraw, float animationSeconds = 0f, float alpha = 1f, bool isEditorMode = false)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        if (!IsAvailable && !isEditorMode)
        {
            return;
        }

        alpha = MathHelper.Clamp(alpha, 0f, 1f);
        if (!IsAvailable && isEditorMode)
        {
            alpha *= 0.45f;
        }

        DrawOrb(spriteBatch, pixel, animationSeconds, alpha);

        if (isEditorMode)
        {
            string label = Type == PowerUpType.Speed ? "SPD" : "JMP";
            Point size = SimpleTextRenderer.MeasureString(label, 1);
            SimpleTextRenderer.DrawString(
                spriteBatch,
                pixel,
                label,
                new Vector2(Center.X - (size.X * 0.5f), Bounds.Top - size.Y - 4),
                1,
                GetFillColor() * alpha);
        }

        if (debugDraw)
        {
            DrawHelper.DrawBorder(spriteBatch, pixel, TriggerBounds, Color.White * alpha, 1);
        }
    }

    public static void DrawIcon(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, PowerUpType type = PowerUpType.Speed, float alpha = 1f)
    {
        PowerUp icon = new(bounds, type);
        icon.Draw(spriteBatch, pixel, debugDraw: false, animationSeconds: 0.35f, alpha, isEditorMode: false);
    }

    public static float ClampDuration(float value) =>
        MathHelper.Clamp(value, MinDurationSeconds, MaxDurationSeconds);

    public static float ClampMultiplier(float value) =>
        MathHelper.Clamp(value, MinMultiplier, MaxMultiplier);

    public static float ClampRespawn(float value) => MathF.Max(0f, value);

    public static PowerUpType CycleType(PowerUpType type) =>
        type == PowerUpType.Speed ? PowerUpType.Jump : PowerUpType.Speed;

    private void DrawOrb(SpriteBatch spriteBatch, Texture2D pixel, float animationSeconds, float alpha)
    {
        int size = Math.Max(6, Math.Min(Bounds.Width, Bounds.Height));
        float pulse = 0.5f + (0.5f * MathF.Sin(animationSeconds * 5f));
        int diameter = Math.Max(6, (int)MathF.Round(size * MathHelper.Lerp(0.72f, 0.92f, pulse)));
        Rectangle orb = new(
            Bounds.Center.X - (diameter / 2),
            Bounds.Center.Y - (diameter / 2),
            diameter,
            diameter);

        Color fill = GetFillColor() * alpha;
        Color rim = Color.Lerp(fill, Color.White, 0.35f) * alpha;
        Color core = Color.Lerp(fill, Color.White, 0.65f) * alpha;

        spriteBatch.Draw(pixel, orb, fill);
        DrawHelper.DrawBorder(spriteBatch, pixel, orb, rim, Math.Max(1, diameter / 8));

        int coreSize = Math.Max(2, diameter / 3);
        Rectangle coreBounds = new(
            orb.Center.X - (coreSize / 2),
            orb.Center.Y - (coreSize / 2) - Math.Max(1, diameter / 10),
            coreSize,
            coreSize);
        spriteBatch.Draw(pixel, coreBounds, core);

        if (Type == PowerUpType.Speed)
        {
            DrawSpeedMark(spriteBatch, pixel, orb, alpha);
        }
        else
        {
            DrawJumpMark(spriteBatch, pixel, orb, alpha);
        }
    }

    private static void DrawSpeedMark(SpriteBatch spriteBatch, Texture2D pixel, Rectangle orb, float alpha)
    {
        Color mark = Color.White * (0.85f * alpha);
        int midY = orb.Center.Y;
        int left = orb.Left + Math.Max(2, orb.Width / 5);
        int right = orb.Right - Math.Max(2, orb.Width / 5);
        spriteBatch.Draw(pixel, new Rectangle(left, midY - 1, Math.Max(1, right - left), 2), mark);
        spriteBatch.Draw(pixel, new Rectangle(right - 3, midY - 4, 2, 2), mark);
        spriteBatch.Draw(pixel, new Rectangle(right - 3, midY + 2, 2, 2), mark);
    }

    private static void DrawJumpMark(SpriteBatch spriteBatch, Texture2D pixel, Rectangle orb, float alpha)
    {
        Color mark = Color.White * (0.85f * alpha);
        int midX = orb.Center.X;
        int top = orb.Top + Math.Max(2, orb.Height / 5);
        int bottom = orb.Bottom - Math.Max(2, orb.Height / 5);
        spriteBatch.Draw(pixel, new Rectangle(midX - 1, top, 2, Math.Max(1, bottom - top)), mark);
        spriteBatch.Draw(pixel, new Rectangle(midX - 4, top + 2, 2, 2), mark);
        spriteBatch.Draw(pixel, new Rectangle(midX + 2, top + 2, 2, 2), mark);
    }

    private Color GetFillColor() =>
        Type == PowerUpType.Speed
            ? ColorPaletteManager.Get(ColorType.Cyan)
            : ColorPaletteManager.Get(ColorType.Yellow);
}
