using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game1_Monogame;

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

    public bool Update(InputManager input)
    {
        IsHovered = Bounds.Contains(input.MousePosition);
        WasClicked = IsHovered && input.LeftMousePressed;
        return WasClicked;
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        // Subtle hover scale: render slightly larger when hovered, but don't modify logical Bounds
        float scale = IsHovered ? 1.06f : 1.0f;
        int scaledW = (int)(Bounds.Width * scale);
        int scaledH = (int)(Bounds.Height * scale);
        int offsetX = (scaledW - Bounds.Width) / 2;
        int offsetY = (scaledH - Bounds.Height) / 2;
        Rectangle drawRect = new Rectangle(Bounds.X - offsetX, Bounds.Y - offsetY, scaledW, scaledH);

        Color fill = IsHovered ? new Color(74, 86, 110) : new Color(52, 61, 80);
        Color border = IsHovered ? new Color(240, 242, 246) : new Color(134, 145, 166);

        spriteBatch.Draw(pixel, drawRect, fill);
        DrawHelper.DrawBorder(spriteBatch, pixel, drawRect, border, 3);

        // Slightly increase text scale when hovered for emphasis
        int textScale = IsHovered ? Math.Max(1, TextScale + 1) : TextScale;
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, Text, drawRect, textScale, Color.White);
    }
}
