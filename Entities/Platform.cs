using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public enum PlatformVerticalDirection
{
    Up,
    Down
}

public enum PlatformHorizontalDirection
{
    Left,
    Right
}

public sealed class Platform
{
    public const int BlockSize = 32;
    public const float DefaultVerticalSpeed = 20f;
    public const float DefaultHorizontalSpeed = 15f;
    public const int DefaultVerticalDistanceBlocks = 2;
    public const int DefaultHorizontalDistanceBlocks = 1;
    public const float SpeedStep = 5f;
    public const float MinSpeed = 0f;
    public const float MaxSpeed = 400f;
    public const int MinDistanceBlocks = 0;
    public const int MaxDistanceBlocks = 64;

    public const float DefaultColorChangePeriodSeconds = 2f;
    public const float ColorChangePeriodStep = 0.25f;
    public const float MinColorChangePeriodSeconds = 0.1f;
    public const float MaxColorChangePeriodSeconds = 30f;
    public const int MinColorCycleLength = 2;
    public const int MaxColorCycleLength = 8;
    public const float ColorChangeWarnSeconds = 3f;

    private GameColor _colorChangeWarnColor = GameColor.Red;
    private float _colorChangeWarnAlpha;
    private float _colorChangeWarnPhase;
    private float _colorChangeWarnLastElapsed = float.NaN;

    public Platform(Rectangle bounds, GameColor color)
    {
        Bounds = bounds;
        HomeX = bounds.X;
        HomeY = bounds.Y;
        PlatformColor = color;
    }

    public Rectangle Bounds { get; set; }
    public int HomeX { get; set; }
    public int HomeY { get; set; }
    public GameColor PlatformColor { get; set; }

    public bool MoveVertical { get; set; }
    public float VerticalSpeed { get; set; } = DefaultVerticalSpeed;
    public int VerticalDistanceBlocks { get; set; } = DefaultVerticalDistanceBlocks;
    public PlatformVerticalDirection VerticalDirection { get; set; } = PlatformVerticalDirection.Up;

    public bool MoveHorizontal { get; set; }
    public float HorizontalSpeed { get; set; } = DefaultHorizontalSpeed;
    public int HorizontalDistanceBlocks { get; set; } = DefaultHorizontalDistanceBlocks;
    public PlatformHorizontalDirection HorizontalDirection { get; set; } = PlatformHorizontalDirection.Right;

    public bool ColorChangeEnabled { get; set; }
    public List<GameColor> ColorCycle { get; set; } = new();
    public float ColorChangePeriodSeconds { get; set; } = DefaultColorChangePeriodSeconds;
    public float ColorChangePhaseSeconds { get; set; }

    public bool HasMotion =>
        (MoveVertical && VerticalSpeed > 0f && VerticalDistanceBlocks > 0)
        || (MoveHorizontal && HorizontalSpeed > 0f && HorizontalDistanceBlocks > 0);

    public bool HasColorChange =>
        ColorChangeEnabled
        && ColorCycle.Count >= MinColorCycleLength
        && ColorChangePeriodSeconds > 0f;

    public float ColorChangeWarnAlpha => _colorChangeWarnAlpha;
    public GameColor ColorChangeWarnColor => _colorChangeWarnColor;

    /// <summary>Only one axis may be active. Enabling one clears the other.</summary>
    public void SetMoveVertical(bool enabled)
    {
        MoveVertical = enabled;
        if (enabled)
        {
            MoveHorizontal = false;
        }
    }

    /// <summary>Only one axis may be active. Enabling one clears the other.</summary>
    public void SetMoveHorizontal(bool enabled)
    {
        MoveHorizontal = enabled;
        if (enabled)
        {
            MoveVertical = false;
        }
    }

    /// <summary>
    /// Enables or disables color cycling. On enable, seeds a valid 2+ cycle from
    /// current <see cref="PlatformColor"/> if needed. On disable, snaps live color to cycle[0].
    /// </summary>
    public void SetColorChangeEnabled(bool enabled)
    {
        ColorChangeEnabled = enabled;
        if (enabled)
        {
            EnsureColorCycleSeeded();
            ColorChangePeriodSeconds = ClampColorChangePeriod(ColorChangePeriodSeconds);
        }
        else if (ColorCycle.Count > 0)
        {
            PlatformColor = ColorCycle[0];
        }
    }

    /// <summary>Keeps authored start color (cycle index 0) in sync with editor color picks.</summary>
    public void SetAuthoredStartColor(GameColor color)
    {
        PlatformColor = color;
        if (ColorCycle.Count == 0)
        {
            ColorCycle.Add(color);
            return;
        }

        ColorCycle[0] = color;
    }

    public void EnsureColorCycleSeeded()
    {
        if (ColorCycle.Count == 0)
        {
            ColorCycle.Add(PlatformColor);
        }

        while (ColorCycle.Count < MinColorCycleLength)
        {
            ColorCycle.Add(NextDistinctCycleColor(ColorCycle[^1]));
        }

        if (ColorCycle.Count > MaxColorCycleLength)
        {
            ColorCycle.RemoveRange(MaxColorCycleLength, ColorCycle.Count - MaxColorCycleLength);
        }
    }

    /// <summary>Editor/authored pose: updates both home and live bounds.</summary>
    public void SetAuthoredBounds(Rectangle bounds)
    {
        HomeX = bounds.X;
        HomeY = bounds.Y;
        Bounds = bounds;
    }

    /// <summary>
    /// Sets live <see cref="PlatformColor"/> from the authored cycle using simulation time.
    /// Looping (not ping-pong): step = floor((t + phase) / period) % count.
    /// Also updates warn-border blink for the last half-step (max <see cref="ColorChangeWarnSeconds"/>).
    /// No-op when cycle disabled or invalid. Independent of motion.
    /// </summary>
    public void ApplyColorAtTime(float timeSeconds)
    {
        _colorChangeWarnAlpha = 0f;
        if (!HasColorChange)
        {
            return;
        }

        float period = ClampColorChangePeriod(ColorChangePeriodSeconds);
        float phase = ColorChangePhaseSeconds;
        if (float.IsNaN(phase) || float.IsInfinity(phase))
        {
            phase = 0f;
        }

        float elapsed = timeSeconds + phase;
        if (elapsed < 0f)
        {
            // Keep deterministic for negative offsets: wrap into a positive domain.
            float cycleSeconds = period * ColorCycle.Count;
            elapsed = elapsed % cycleSeconds;
            if (elapsed < 0f)
            {
                elapsed += cycleSeconds;
            }
        }

        int step = (int)MathF.Floor(elapsed / period) % ColorCycle.Count;
        if (step < 0)
        {
            step += ColorCycle.Count;
        }

        PlatformColor = ColorCycle[step];
        UpdateColorChangeWarn(elapsed, period, step);
    }

    private void UpdateColorChangeWarn(float elapsed, float period, int step)
    {
        float timeInStep = elapsed - MathF.Floor(elapsed / period) * period;
        if (timeInStep < 0f)
        {
            timeInStep += period;
        }

        float remaining = period - timeInStep;
        // Half the color-step duration, capped at 3s.
        float warnWindow = MathF.Min(ColorChangeWarnSeconds, period * 0.5f);
        if (remaining > warnWindow || warnWindow <= 0f)
        {
            _colorChangeWarnPhase = 0f;
            _colorChangeWarnLastElapsed = float.NaN;
            return;
        }

        // 0 at warn start → 1 at color flip.
        float urgency = 1f - (remaining / warnWindow);
        urgency = MathHelper.Clamp(urgency, 0f, 1f);

        // Integrate phase with rising hz so blink starts slow and only speeds up (no sine jumps).
        float dt = 0f;
        if (!float.IsNaN(_colorChangeWarnLastElapsed))
        {
            dt = elapsed - _colorChangeWarnLastElapsed;
            if (dt < 0f || dt > period)
            {
                dt = 0f;
                _colorChangeWarnPhase = 0f;
            }
        }
        else
        {
            _colorChangeWarnPhase = 0f;
        }

        _colorChangeWarnLastElapsed = elapsed;
        float hz = MathHelper.Lerp(1.25f, 9f, urgency * urgency);
        _colorChangeWarnPhase += dt * hz;
        float pulse = 0.5f + 0.5f * MathF.Sin(_colorChangeWarnPhase * MathF.Tau);
        // Soft square-ish snap near the end so flashes read clearer.
        float snap = MathHelper.SmoothStep(0.15f, 0.85f, pulse);
        float peak = MathHelper.Lerp(0.55f, 1f, urgency);

        _colorChangeWarnColor = ColorCycle[(step + 1) % ColorCycle.Count];
        _colorChangeWarnAlpha = snap * peak;
    }

    /// <summary>
    /// Advances live Bounds from authored home using deterministic ping-pong.
    /// Returns pixel delta applied this call (for rider carry).
    /// </summary>
    public Vector2 ApplyMotionAtTime(float timeSeconds)
    {
        Point oldLocation = Bounds.Location;
        int x = HomeX;
        int y = HomeY;

        if (MoveHorizontal && HorizontalSpeed > 0f && HorizontalDistanceBlocks > 0)
        {
            float distancePx = HorizontalDistanceBlocks * BlockSize;
            float offset = PingPongOffset(timeSeconds, HorizontalSpeed, distancePx);
            int signed = (int)MathF.Round(offset);
            x = HorizontalDirection == PlatformHorizontalDirection.Right
                ? HomeX + signed
                : HomeX - signed;
        }
        else if (MoveVertical && VerticalSpeed > 0f && VerticalDistanceBlocks > 0)
        {
            float distancePx = VerticalDistanceBlocks * BlockSize;
            float offset = PingPongOffset(timeSeconds, VerticalSpeed, distancePx);
            int signed = (int)MathF.Round(offset);
            // Screen Y grows downward: Up decreases Y, Down increases Y.
            y = VerticalDirection == PlatformVerticalDirection.Up
                ? HomeY - signed
                : HomeY + signed;
        }

        Bounds = new Rectangle(x, y, Bounds.Width, Bounds.Height);
        return new Vector2(Bounds.X - oldLocation.X, Bounds.Y - oldLocation.Y);
    }

    public Rectangle GetMotionExtents()
    {
        int left = HomeX;
        int top = HomeY;
        int right = HomeX + Bounds.Width;
        int bottom = HomeY + Bounds.Height;

        if (MoveHorizontal && HorizontalDistanceBlocks > 0)
        {
            int travel = HorizontalDistanceBlocks * BlockSize;
            if (HorizontalDirection == PlatformHorizontalDirection.Right)
            {
                right += travel;
            }
            else
            {
                left -= travel;
            }
        }
        else if (MoveVertical && VerticalDistanceBlocks > 0)
        {
            int travel = VerticalDistanceBlocks * BlockSize;
            if (VerticalDirection == PlatformVerticalDirection.Up)
            {
                top -= travel;
            }
            else
            {
                bottom += travel;
            }
        }

        return new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }

    public Point GetMotionEndLocation()
    {
        int x = HomeX;
        int y = HomeY;

        if (MoveHorizontal && HorizontalDistanceBlocks > 0)
        {
            int travel = HorizontalDistanceBlocks * BlockSize;
            x = HorizontalDirection == PlatformHorizontalDirection.Right
                ? HomeX + travel
                : HomeX - travel;
        }
        else if (MoveVertical && VerticalDistanceBlocks > 0)
        {
            int travel = VerticalDistanceBlocks * BlockSize;
            y = VerticalDirection == PlatformVerticalDirection.Up
                ? HomeY - travel
                : HomeY + travel;
        }

        return new Point(x, y);
    }

    public static float ClampSpeed(float speed) =>
        MathHelper.Clamp(speed, MinSpeed, MaxSpeed);

    public static int ClampDistanceBlocks(int blocks) =>
        Math.Clamp(blocks, MinDistanceBlocks, MaxDistanceBlocks);

    public static float ClampColorChangePeriod(float seconds) =>
        MathHelper.Clamp(seconds, MinColorChangePeriodSeconds, MaxColorChangePeriodSeconds);

    public static GameColor NextDistinctCycleColor(GameColor current) =>
        current switch
        {
            GameColor.Red => GameColor.Blue,
            GameColor.Blue => GameColor.Green,
            GameColor.Green => GameColor.Red,
            _ => GameColor.Red
        };

    /// <summary>Triangle wave over [0, distance]: 0 → distance → 0.</summary>
    public static float PingPongOffset(float timeSeconds, float speedPxPerSec, float distancePx)
    {
        if (speedPxPerSec <= 0f || distancePx <= 0f)
        {
            return 0f;
        }

        float cycle = distancePx * 2f;
        float phase = timeSeconds * speedPxPerSec;
        phase -= MathF.Floor(phase / cycle) * cycle;
        return phase <= distancePx ? phase : (cycle - phase);
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, bool debugDraw)
    {
        spriteBatch.Draw(pixel, Bounds, PlatformColor.ToXnaColor());
        DrawHelper.DrawBorder(spriteBatch, pixel, Bounds, Color.Black, 2);

        if (_colorChangeWarnAlpha > 0.01f)
        {
            Color warn = _colorChangeWarnColor.ToXnaColor() * _colorChangeWarnAlpha;
            warn.A = (byte)MathHelper.Clamp((int)(255f * _colorChangeWarnAlpha), 0, 255);
            DrawHelper.DrawBorder(spriteBatch, pixel, Bounds, warn, 7);
        }

        if (debugDraw)
        {
            DrawHelper.DrawBorder(spriteBatch, pixel, Bounds, Color.White, 1);
        }
    }
}
