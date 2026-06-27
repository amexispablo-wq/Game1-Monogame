#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public sealed class Slider
{
    public string Label { get; set; } = string.Empty;
    public float Value { get; set; }
    public float MinValue { get; set; }
    public float MaxValue { get; set; }
    public Rectangle Bounds { get; set; }
    public Rectangle SliderTrackBounds { get; private set; }
    public Rectangle SliderThumbBounds { get; private set; }
    public bool IsHovered { get; private set; }
    public bool IsActive { get; private set; }

    public Slider(string label, float initialValue, float minValue = 0f, float maxValue = 1f)
    {
        Label = label;
        Value = MathHelper.Clamp(initialValue, minValue, maxValue);
        MinValue = minValue;
        MaxValue = maxValue;
    }

    public void Update(InputManager input, InputNavigationService navigation)
    {
        CalculateBounds();

        bool pointerOver = Bounds.Contains(input.UiPointerPosition)
            || SliderTrackBounds.Contains(input.UiPointerPosition)
            || SliderThumbBounds.Contains(input.UiPointerPosition);
        IsHovered = navigation.AllowPointerHoverVisual && pointerOver;

        if (input.UiPointerPressed && IsHovered)
        {
            IsActive = true;
            UpdateValueFromMouse(input.UiPointerPosition.X);
        }

        if (input.LeftMouseReleased)
        {
            IsActive = false;
        }

        if (IsActive && input.UiPointerHeld)
        {
            UpdateValueFromMouse(input.UiPointerPosition.X);
        }
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        CalculateBounds();

        if (!string.IsNullOrWhiteSpace(Label))
        {
            Rectangle labelBounds = new(Bounds.X, Bounds.Y, Bounds.Width, 24);
            SimpleTextRenderer.DrawLeft(spriteBatch, pixel, Label, labelBounds.Location.ToVector2(), 2, Color.White);
        }

        spriteBatch.Draw(pixel, new Rectangle(SliderTrackBounds.X + 2, SliderTrackBounds.Y + 3, SliderTrackBounds.Width, SliderTrackBounds.Height), new Color(4, 6, 10, 90));
        spriteBatch.Draw(pixel, SliderTrackBounds, new Color(31, 39, 57));
        DrawHelper.DrawBorder(spriteBatch, pixel, SliderTrackBounds, new Color(95, 111, 140), 2);

        float ratio = GetValueRatio();
        int fillWidth = (int)(SliderTrackBounds.Width * ratio);
        Rectangle fillBounds = new(SliderTrackBounds.X, SliderTrackBounds.Y, fillWidth, SliderTrackBounds.Height);
        spriteBatch.Draw(pixel, fillBounds, new Color(82, 176, 214));

        Color thumbColor = IsActive
            ? new Color(255, 226, 122)
            : (IsHovered ? new Color(198, 231, 246) : new Color(146, 202, 226));
        spriteBatch.Draw(pixel, SliderThumbBounds, thumbColor);
        DrawHelper.DrawBorder(spriteBatch, pixel, SliderThumbBounds, Color.White, IsActive || IsHovered ? 2 : 1);

        string percentage = $"{(int)(Value * 100)}%";
        Rectangle percentBounds = new(Bounds.Right - 58, Bounds.Y, 58, Bounds.Height);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, percentage, percentBounds, 2, new Color(210, 220, 238));
    }

    private void CalculateBounds()
    {
        int percentWidth = 58;
        int trackGap = 12;
        int trackHeight = 14;
        int trackWidth = System.Math.Max(1, Bounds.Width - percentWidth - trackGap);
        int trackY = Bounds.Center.Y - (trackHeight / 2);

        SliderTrackBounds = new Rectangle(Bounds.X, trackY, trackWidth, trackHeight);

        float ratio = GetValueRatio();
        int thumbWidth = 16;
        int thumbHeight = 28;
        int thumbX = SliderTrackBounds.X + (int)(SliderTrackBounds.Width * ratio) - (thumbWidth / 2);
        int thumbY = Bounds.Center.Y - (thumbHeight / 2);
        SliderThumbBounds = new Rectangle(thumbX, thumbY, thumbWidth, thumbHeight);
    }

    private void UpdateValueFromMouse(int mouseX)
    {
        int relativeX = mouseX - SliderTrackBounds.X;
        float ratio = MathHelper.Clamp(relativeX / (float)SliderTrackBounds.Width, 0f, 1f);
        Value = MinValue + (ratio * (MaxValue - MinValue));
    }

    private float GetValueRatio()
    {
        if (MaxValue <= MinValue)
        {
            return 0f;
        }

        return MathHelper.Clamp((Value - MinValue) / (MaxValue - MinValue), 0f, 1f);
    }
}
