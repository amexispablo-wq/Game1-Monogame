#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game1_Monogame;

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

    private int _selectedIndex = 0;
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

    public void Update(InputManager input)
    {
        CalculateBounds();

        _leftArrowHovered = _leftArrowBounds.Contains(input.MousePosition);
        _rightArrowHovered = _rightArrowBounds.Contains(input.MousePosition);

        if (input.LeftMousePressed)
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

        // Draw background
        spriteBatch.Draw(pixel, Bounds, new Color(50, 60, 80));
        DrawHelper.DrawBorder(spriteBatch, pixel, Bounds, new Color(80, 90, 110), 1);

        // Draw left arrow
        Color leftArrowColor = _leftArrowHovered ? new Color(200, 200, 200) : new Color(134, 145, 166);
        spriteBatch.Draw(pixel, _leftArrowBounds, new Color(60, 70, 90));
        DrawHelper.DrawBorder(spriteBatch, pixel, _leftArrowBounds, leftArrowColor, 1);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, "◀", _leftArrowBounds, 2, leftArrowColor);

        // Draw center display
        spriteBatch.Draw(pixel, _displayBounds, new Color(45, 52, 70));
        string displayText = DisplayFunc(CurrentOption);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, displayText, _displayBounds, 2, Color.White);

        // Draw right arrow
        Color rightArrowColor = _rightArrowHovered ? new Color(200, 200, 200) : new Color(134, 145, 166);
        spriteBatch.Draw(pixel, _rightArrowBounds, new Color(60, 70, 90));
        DrawHelper.DrawBorder(spriteBatch, pixel, _rightArrowBounds, rightArrowColor, 1);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, "▶", _rightArrowBounds, 2, rightArrowColor);
    }

    private void CalculateBounds()
    {
        int arrowWidth = 35;
        int padding = 2;

        _leftArrowBounds = new Rectangle(
            Bounds.X + padding,
            Bounds.Y + padding,
            arrowWidth,
            Bounds.Height - (padding * 2)
        );

        _rightArrowBounds = new Rectangle(
            Bounds.Right - arrowWidth - padding,
            Bounds.Y + padding,
            arrowWidth,
            Bounds.Height - (padding * 2)
        );

        _displayBounds = new Rectangle(
            _leftArrowBounds.Right + padding,
            Bounds.Y + padding,
            _rightArrowBounds.Left - _leftArrowBounds.Right - (padding * 2),
            Bounds.Height - (padding * 2)
        );
    }
}
