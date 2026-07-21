using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public sealed class Checkbox
{
    public string Label { get; set; } = string.Empty;
    public Rectangle Bounds { get; set; }
    public bool IsChecked { get; set; }
    public bool IsEnabled { get; set; } = true;

    private bool _isHovered;
    private bool _pointerInside;

    public bool Update(InputManager input, InputNavigationService navigation)
    {
        bool pointerOver = Bounds.Contains(input.UiPointerPosition);
        if (pointerOver && !_pointerInside && IsEnabled)
        {
            GameAudio.PlayMenuHover();
        }

        _pointerInside = pointerOver;
        _isHovered = navigation.AllowPointerHoverVisual && pointerOver;
        if (!IsEnabled)
        {
            return false;
        }

        if (_pointerInside && input.UiPointerPressed)
        {
            IsChecked = !IsChecked;
            GameAudio.PlayMenuPress();
            return true;
        }

        return false;
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        Color fill = IsEnabled ? new Color(36, 44, 58) : new Color(28, 32, 40);
        Color border = _isHovered && IsEnabled ? new Color(255, 226, 122) : new Color(95, 110, 135);
        Color tickColor = IsEnabled ? Color.White : new Color(180, 190, 210);
        Color labelColor = IsEnabled ? Color.White : new Color(140, 150, 170);

        Rectangle box = new Rectangle(Bounds.X, Bounds.Y, 24, 24);
        spriteBatch.Draw(pixel, box, fill);
        DrawHelper.DrawBorder(spriteBatch, pixel, box, border, _isHovered && IsEnabled ? 3 : 2);

        if (IsChecked)
        {
            Rectangle check = new Rectangle(box.X + 6, box.Y + 6, 12, 12);
            spriteBatch.Draw(pixel, check, tickColor);
        }

        var labelPosition = new Vector2(Bounds.X + 32, Bounds.Y + 2);
        SimpleTextRenderer.DrawString(spriteBatch, pixel, Label, labelPosition, 2, labelColor);
    }
}
