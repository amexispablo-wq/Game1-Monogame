#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game1_Monogame;

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

    public void Update(InputManager input)
    {
        IsHovered = Bounds.Contains(input.MousePosition);

        if (input.LeftMousePressed && IsHovered)
        {
            IsActive = true;
        }

        if (input.LeftMouseReleased)
        {
            IsActive = false;
        }

        if (IsActive && input.LeftMouseHeld)
        {
            UpdateValueFromMouse(input.MousePosition.X);
        }
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        // Draw label
        Rectangle labelBounds = new(Bounds.X, Bounds.Y, Bounds.Width, 30);
        SimpleTextRenderer.DrawLeft(spriteBatch, pixel, Label, labelBounds.Location.ToVector2(), 2, Color.White);

        // Draw percentage
        string percentage = $"{(int)(Value * 100)}%";
        Rectangle percentBounds = new(Bounds.X + Bounds.Width - 100, Bounds.Y, 100, 30);
        SimpleTextRenderer.DrawRight(spriteBatch, pixel, percentage, new Vector2(percentBounds.Right - 10, Bounds.Y + 10), 2, Color.LightGray);

        // Draw track background
        int trackY = Bounds.Y + 35;
        int trackHeight = 12;
        Rectangle trackBgBounds = new(Bounds.X, trackY, Bounds.Width, trackHeight);
        spriteBatch.Draw(pixel, trackBgBounds, new Color(40, 50, 70));
        DrawHelper.DrawBorder(spriteBatch, pixel, trackBgBounds, new Color(80, 90, 110), 1);

        // Draw filled portion
        float fillWidth = (Value - MinValue) / (MaxValue - MinValue) * Bounds.Width;
        Rectangle fillBounds = new(Bounds.X, trackY, (int)fillWidth, trackHeight);
        spriteBatch.Draw(pixel, fillBounds, new Color(100, 180, 255));

        // Draw thumb
        int thumbX = (int)(Bounds.X + fillWidth - 5);
        int thumbWidth = 10;
        Rectangle thumbBounds = new(thumbX, trackY - 2, thumbWidth, trackHeight + 4);
        Color thumbColor = IsActive ? new Color(150, 200, 255) : new Color(100, 150, 200);
        spriteBatch.Draw(pixel, thumbBounds, thumbColor);
        DrawHelper.DrawBorder(spriteBatch, pixel, thumbBounds, new Color(200, 220, 255), 1);

        SliderTrackBounds = trackBgBounds;
        SliderThumbBounds = thumbBounds;
    }

    private void UpdateValueFromMouse(int mouseX)
    {
        int relativeX = mouseX - Bounds.X;
        float ratio = MathHelper.Clamp(relativeX / (float)Bounds.Width, 0f, 1f);
        Value = MinValue + (ratio * (MaxValue - MinValue));
    }
}
