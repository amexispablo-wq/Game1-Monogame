#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

/// <summary>
/// Generic cycle selector component for cycling through a list of options using arrow buttons.
/// </summary>
public sealed class CycleSelector<T> where T : notnull
{
    public List<T> Options { get; }
    public T CurrentOption
    {
        get => Options.Count > 0 ? Options[_selectedIndex] : throw new InvalidOperationException("No options available");
        set
        {
            int index = Options.IndexOf(value);
            if (index >= 0)
                _selectedIndex = index;
        }
    }

    public Rectangle Bounds { get; set; }
    public Func<T, string> DisplayFunc { get; set; } = obj => obj.ToString() ?? "Unknown";

    private int _selectedIndex;
    private Rectangle _leftArrowBounds;
    private Rectangle _rightArrowBounds;
    private Rectangle _displayBounds;
    private bool _leftArrowHovered;
    private bool _rightArrowHovered;

    public CycleSelector(List<T> options, Func<T, string>? displayFunc = null)
    {
        if (options.Count == 0)
            throw new ArgumentException("Options list must contain at least one item", nameof(options));

        Options = options;
        if (displayFunc != null)
            DisplayFunc = displayFunc;
    }

    public void Update(InputManager input, InputNavigationService navigation)
    {
        CalculateBounds();

        bool allowHover = navigation.AllowPointerHoverVisual;
        _leftArrowHovered = allowHover && _leftArrowBounds.Contains(input.UiPointerPosition);
        _rightArrowHovered = allowHover && _rightArrowBounds.Contains(input.UiPointerPosition);

        if (input.UiPointerPressed)
        {
            if (_leftArrowHovered)
            {
                _selectedIndex = (_selectedIndex - 1 + Options.Count) % Options.Count;
            }
            else if (_rightArrowHovered)
            {
                _selectedIndex = (_selectedIndex + 1) % Options.Count;
            }
        }
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        CalculateBounds();

        spriteBatch.Draw(pixel, new Rectangle(Bounds.X + 3, Bounds.Y + 4, Bounds.Width, Bounds.Height), new Color(4, 6, 10, 90));
        spriteBatch.Draw(pixel, Bounds, new Color(46, 56, 76));
        DrawHelper.DrawBorder(spriteBatch, pixel, Bounds, new Color(105, 121, 150), 2);

        Color leftArrowColor = _leftArrowHovered ? new Color(255, 226, 122) : new Color(184, 196, 216);
        Color rightArrowColor = _rightArrowHovered ? new Color(255, 226, 122) : new Color(184, 196, 216);
        Color arrowFill = new Color(58, 70, 94);
        Color arrowHoverFill = new Color(78, 94, 126);

        spriteBatch.Draw(pixel, _leftArrowBounds, _leftArrowHovered ? arrowHoverFill : arrowFill);
        DrawHelper.DrawBorder(spriteBatch, pixel, _leftArrowBounds, leftArrowColor, _leftArrowHovered ? 2 : 1);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, "<", _leftArrowBounds, 3, leftArrowColor);

        spriteBatch.Draw(pixel, _displayBounds, new Color(35, 43, 60));
        string displayText = DisplayFunc(CurrentOption);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, displayText, _displayBounds, GetFittedTextScale(displayText, _displayBounds, 2), Color.White);

        spriteBatch.Draw(pixel, _rightArrowBounds, _rightArrowHovered ? arrowHoverFill : arrowFill);
        DrawHelper.DrawBorder(spriteBatch, pixel, _rightArrowBounds, rightArrowColor, _rightArrowHovered ? 2 : 1);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, ">", _rightArrowBounds, 3, rightArrowColor);
    }

    private void CalculateBounds()
    {
        int arrowWidth = Math.Clamp(Bounds.Height + 8, 48, 64);
        int padding = 4;

        _leftArrowBounds = new Rectangle(
            Bounds.X + padding,
            Bounds.Y + padding,
            arrowWidth,
            Bounds.Height - (padding * 2));

        _rightArrowBounds = new Rectangle(
            Bounds.Right - arrowWidth - padding,
            Bounds.Y + padding,
            arrowWidth,
            Bounds.Height - (padding * 2));

        _displayBounds = new Rectangle(
            _leftArrowBounds.Right + padding,
            Bounds.Y + padding,
            _rightArrowBounds.Left - _leftArrowBounds.Right - (padding * 2),
            Bounds.Height - (padding * 2));
    }

    private static int GetFittedTextScale(string text, Rectangle bounds, int preferredScale)
    {
        int scale = Math.Max(1, preferredScale);
        int maxWidth = Math.Max(1, bounds.Width - 12);
        int maxHeight = Math.Max(1, bounds.Height - 8);

        while (scale > 1)
        {
            Point size = SimpleTextRenderer.MeasureString(text, scale);
            if (size.X <= maxWidth && size.Y <= maxHeight)
            {
                break;
            }

            scale--;
        }

        return scale;
    }
}
