using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public sealed class DeveloperTuningPanel
{
    private sealed class TuningField
    {
        public required string Label { get; init; }
        public required Func<float> Getter { get; init; }
        public required Action<float> Setter { get; init; }
        public required float Min { get; init; }
        public required float Max { get; init; }
        public float Step { get; init; } = 1f;
        public int Decimals { get; init; } = 1;
    }

    private readonly List<TuningField> _fields = new();
    private float _scrollOffset;
    private int _activeField = -1;

    public DeveloperTuningPanel()
    {
        GameplayTuning tuning = GameplayTuning.Active;
        _fields.AddRange(new[]
        {
            Field("Player Mass", () => tuning.PlayerMass, v => tuning.PlayerMass = v, 0.2f, 4f, 0.1f, 2),
            Field("Ground Friction", () => tuning.GroundFriction, v => tuning.GroundFriction = v, 0f, 40f, 0.5f, 1),
            Field("Air Control", () => tuning.AirAcceleration, v => tuning.AirAcceleration = v, 100f, 3000f, 25f, 0),
            Field("Ground Accel", () => tuning.GroundAcceleration, v => tuning.GroundAcceleration = v, 200f, 5000f, 50f, 0),
            Field("Jump Force", () => tuning.JumpImpulse, v => tuning.JumpImpulse = v, 100f, 1200f, 10f, 0),
            Field("Rope Rest Length", () => tuning.RopeRestLength, v => tuning.RopeRestLength = v, 80f, 600f, 5f, 0),
            Field("Min Rope Length", () => tuning.MinimumRopeLength, v => tuning.MinimumRopeLength = v, 40f, 400f, 5f, 0),
            Field("Max Rope Length", () => tuning.MaximumRopeLength, v => tuning.MaximumRopeLength = v, 80f, 700f, 5f, 0),
            Field("Slack Distance", () => tuning.SlackDistance, v => tuning.SlackDistance = v, 0f, 200f, 2f, 0),
            Field("Rope Stiffness", () => tuning.RopeStiffness, v => tuning.RopeStiffness = v, 0.05f, 1f, 0.02f, 2),
            Field("Rope Damping", () => tuning.RopeDamping, v => tuning.RopeDamping = v, 0.7f, 1f, 0.01f, 2),
            Field("Constraint Iterations", () => tuning.ConstraintIterations, v => tuning.ConstraintIterations = Math.Clamp((int)MathF.Round(v), 1, 32), 1f, 24f, 1f, 0),
            Field("Node Mass", () => tuning.NodeMass, v => tuning.NodeMass = v, 0f, 2f, 0.05f, 2),
            Field("Pull Shorten Speed", () => tuning.PullShorteningSpeed, v => tuning.PullShorteningSpeed = v, 20f, 600f, 10f, 0),
            Field("Pull Recovery Speed", () => tuning.PullRecoverySpeed, v => tuning.PullRecoverySpeed = v, 20f, 600f, 10f, 0),
            Field("Max Rope Force", () => tuning.MaxRopeForce, v => tuning.MaxRopeForce = v, 200f, 12000f, 100f, 0),
            Field("Max Pull Force", () => tuning.MaxPullForce, v => tuning.MaxPullForce = v, 200f, 12000f, 100f, 0),
            Field("Tension Curve", () => tuning.ProgressiveTensionCurve, v => tuning.ProgressiveTensionCurve = v, 0.5f, 6f, 0.1f, 1),
            Field("Launch Force Mult", () => tuning.LaunchForceMultiplier, v => tuning.LaunchForceMultiplier = v, 0f, 3f, 0.05f, 2),
            Field("Node Count", () => tuning.NodeCount, v => tuning.NodeCount = (int)MathF.Round(v), 4f, 48f, 1f, 0),
            Field("Max Correction", () => tuning.MaxCorrectionPerFrame, v => tuning.MaxCorrectionPerFrame = v, 1f, 30f, 0.5f, 1)
        });
    }

    public bool IsVisible { get; set; }

    public void Toggle()
    {
        IsVisible = !IsVisible;
    }

    public void Update(GameTime gameTime, InputManager input, Viewport viewport, PartyManager party)
    {
        if (!IsVisible)
        {
            _activeField = -1;
            return;
        }

        if (input.MouseWheelDelta != 0)
        {
            _scrollOffset = MathF.Max(0f, _scrollOffset - (input.MouseWheelDelta * 0.15f));
        }

        Rectangle panelBounds = GetPanelBounds(viewport, party);
        int rowHeight = 34;
        int contentHeight = _fields.Count * rowHeight;
        float maxScroll = MathF.Max(0f, contentHeight - (panelBounds.Height - 40));
        _scrollOffset = MathHelper.Clamp(_scrollOffset, 0f, maxScroll);

        if (input.LeftMouseReleased)
        {
            _activeField = -1;
        }

        for (int i = 0; i < _fields.Count; i++)
        {
            Rectangle rowBounds = GetRowBounds(panelBounds, i, rowHeight);
            if (!rowBounds.Intersects(panelBounds))
            {
                continue;
            }

            Rectangle trackBounds = GetTrackBounds(rowBounds);
            if (input.LeftMousePressed && trackBounds.Contains(input.UiPointerPosition))
            {
                _activeField = i;
                ApplyPointer(_fields[i], input.UiPointerPosition.X, trackBounds);
            }

            if (_activeField == i && input.UiPointerHeld)
            {
                ApplyPointer(_fields[i], input.UiPointerPosition.X, trackBounds);
            }
        }
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport, PartyManager party)
    {
        if (!IsVisible)
        {
            return;
        }

        Rectangle panelBounds = GetPanelBounds(viewport, party);
        spriteBatch.Draw(pixel, panelBounds, new Color(10, 14, 24, 220));
        DrawHelper.DrawBorder(spriteBatch, pixel, panelBounds, new Color(120, 180, 220), 2);
        SimpleTextRenderer.DrawString(spriteBatch, pixel, "DEV TUNING (F6)", new Vector2(panelBounds.X + 10, panelBounds.Y + 8), 1, Color.White);

        int rowHeight = 34;
        for (int i = 0; i < _fields.Count; i++)
        {
            Rectangle rowBounds = GetRowBounds(panelBounds, i, rowHeight);
            if (!rowBounds.Intersects(panelBounds))
            {
                continue;
            }

            TuningField field = _fields[i];
            float value = field.Getter();
            string label = $"{field.Label}: {FormatValue(value, field.Decimals)}";
            SimpleTextRenderer.DrawString(spriteBatch, pixel, label, new Vector2(rowBounds.X + 8, rowBounds.Y + 2), 1, Color.White);

            Rectangle trackBounds = GetTrackBounds(rowBounds);
            spriteBatch.Draw(pixel, trackBounds, new Color(28, 36, 52));
            DrawHelper.DrawBorder(spriteBatch, pixel, trackBounds, new Color(90, 110, 140), 1);

            float ratio = InverseLerp(field.Min, field.Max, value);
            Rectangle fillBounds = new(trackBounds.X, trackBounds.Y, (int)(trackBounds.Width * ratio), trackBounds.Height);
            spriteBatch.Draw(pixel, fillBounds, new Color(82, 176, 214));
        }
    }

    private static TuningField Field(
        string label,
        Func<float> getter,
        Action<float> setter,
        float min,
        float max,
        float step,
        int decimals)
    {
        return new TuningField
        {
            Label = label,
            Getter = getter,
            Setter = setter,
            Min = min,
            Max = max,
            Step = step,
            Decimals = decimals
        };
    }

    private static void ApplyPointer(TuningField field, int pointerX, Rectangle trackBounds)
    {
        float ratio = MathHelper.Clamp((pointerX - trackBounds.X) / (float)trackBounds.Width, 0f, 1f);
        float raw = field.Min + (ratio * (field.Max - field.Min));
        if (field.Step > 0f)
        {
            raw = MathF.Round(raw / field.Step) * field.Step;
        }

        field.Setter(MathHelper.Clamp(raw, field.Min, field.Max));
    }

    private static Rectangle GetPanelBounds(Viewport viewport, PartyManager party)
    {
        int width = Math.Min(360, Math.Max(280, viewport.Width / 4));
        Rectangle partyBounds = PartyHudOverlay.GetPanelBounds(viewport, party);
        int top = partyBounds == Rectangle.Empty ? 12 : partyBounds.Bottom + 8;
        int height = Math.Max(180, viewport.Height - top - 12);
        return new Rectangle(viewport.Width - width - 12, top, width, height);
    }

    private Rectangle GetRowBounds(Rectangle panelBounds, int index, int rowHeight)
    {
        int y = panelBounds.Y + 30 + (index * rowHeight) - (int)_scrollOffset;
        return new Rectangle(panelBounds.X + 8, y, panelBounds.Width - 16, rowHeight - 4);
    }

    private static Rectangle GetTrackBounds(Rectangle rowBounds)
    {
        return new Rectangle(rowBounds.X, rowBounds.Bottom - 12, rowBounds.Width, 10);
    }

    private static float InverseLerp(float min, float max, float value)
    {
        if (max <= min)
        {
            return 0f;
        }

        return MathHelper.Clamp((value - min) / (max - min), 0f, 1f);
    }

    private static string FormatValue(float value, int decimals)
    {
        return decimals <= 0
            ? ((int)MathF.Round(value)).ToString()
            : value.ToString($"F{decimals}");
    }
}
