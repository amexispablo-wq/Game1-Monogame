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
        Color fill = IsHovered ? new Color(74, 86, 110) : new Color(52, 61, 80);
        Color border = IsHovered ? new Color(240, 242, 246) : new Color(134, 145, 166);

        spriteBatch.Draw(pixel, Bounds, fill);
        DrawHelper.DrawBorder(spriteBatch, pixel, Bounds, border, 3);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, Text, Bounds, TextScale, Color.White);
    }
}
