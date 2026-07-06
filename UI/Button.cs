using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public sealed class Button
{
    public Button(string text)
    {
        Text = text;
    }

    public string Text { get; }
    public Rectangle Bounds { get; set; }
    public bool IsHovered { get; private set; }
    public bool WasClicked { get; private set; }
    public int TextScale { get; set; } = 4;
    public Color FillColor { get; set; } = new(52, 61, 80);
    public Color HoverFillColor { get; set; } = new(74, 86, 110);
    public Color BorderColor { get; set; } = new(134, 145, 166);
    public Color HoverBorderColor { get; set; } = new(240, 242, 246);
    public Color TextColor { get; set; } = Color.White;
    public Color HoverTextColor { get; set; } = Color.White;

    public bool Update(InputManager input, InputNavigationService navigation)
    {
        bool pointerOver = Bounds.Contains(input.UiPointerPosition);
        IsHovered = navigation.AllowPointerHoverVisual && pointerOver;
        WasClicked = pointerOver && input.UiPointerPressed;
        return WasClicked;
    }

    public void SetPointerHover(bool hovered)
    {
        IsHovered = hovered;
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        // Subtle hover scale: render slightly larger when hovered, but don't modify logical Bounds
        float scale = IsHovered ? 1.03f : 1.0f;
        int scaledW = (int)(Bounds.Width * scale);
        int scaledH = (int)(Bounds.Height * scale);
        int offsetX = (scaledW - Bounds.Width) / 2;
        int offsetY = (scaledH - Bounds.Height) / 2;
        Rectangle drawRect = new Rectangle(Bounds.X - offsetX, Bounds.Y - offsetY, scaledW, scaledH);

        Color fill = IsHovered ? HoverFillColor : FillColor;
        Color border = IsHovered ? HoverBorderColor : BorderColor;
        Color text = IsHovered ? HoverTextColor : TextColor;

        spriteBatch.Draw(pixel, new Rectangle(drawRect.X + 4, drawRect.Y + 5, drawRect.Width, drawRect.Height), new Color(5, 7, 12, 95));
        spriteBatch.Draw(pixel, drawRect, fill);
        DrawHelper.DrawBorder(spriteBatch, pixel, drawRect, border, IsHovered ? 3 : 2);

        int textScale = GetFittedTextScale(drawRect);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, Text, drawRect, textScale, text);
    }

    private int GetFittedTextScale(Rectangle drawRect)
    {
        int scale = Math.Max(1, TextScale);
        int maxWidth = Math.Max(1, drawRect.Width - 24);
        int maxHeight = Math.Max(1, drawRect.Height - 12);

        while (scale > 1)
        {
            Point size = SimpleTextRenderer.MeasureString(Text, scale);
            if (size.X <= maxWidth && size.Y <= maxHeight)
            {
                break;
            }

            scale--;
        }

        return scale;
    }
}
