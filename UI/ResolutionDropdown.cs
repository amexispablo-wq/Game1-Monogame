#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public sealed class Resolution
{
    public int Width { get; set; }
    public int Height { get; set; }

    public Resolution(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public override string ToString() => $"{Width}x{Height}";
    public override bool Equals(object? obj) => obj is Resolution r && r.Width == Width && r.Height == Height;
    public override int GetHashCode() => Width.GetHashCode() ^ Height.GetHashCode();
}

public sealed class ResolutionDropdown
{
    public string Label { get; set; } = "Resolution";
    public Rectangle Bounds { get; set; }
    public List<Resolution> Resolutions { get; private set; } = new();
    public Resolution? SelectedResolution { get; set; }
    public bool IsExpanded { get; set; }
    public int? HighlightedIndex { get; set; }
    public int DropdownHeight { get; set; } = 150;
    public bool OpenUpwards { get; set; }

    public ResolutionDropdown()
    {
        InitializeResolutions();
    }

    private void InitializeResolutions()
    {
        // 16:9
        Resolutions.Add(new Resolution(1280, 720));
        Resolutions.Add(new Resolution(1600, 900));
        Resolutions.Add(new Resolution(1920, 1080));
        Resolutions.Add(new Resolution(2560, 1440));

        // Ultrawide
        Resolutions.Add(new Resolution(2560, 1080));
        Resolutions.Add(new Resolution(3440, 1440));

        // 4:3
        Resolutions.Add(new Resolution(1024, 768));
        Resolutions.Add(new Resolution(1600, 1200));
    }

    public void Update(InputManager input)
    {
        Rectangle headerBounds = Bounds;

        if (input.LeftMousePressed && headerBounds.Contains(input.MousePosition))
        {
            IsExpanded = !IsExpanded;
            HighlightedIndex = null;
            return;
        }

        if (!IsExpanded)
        {
            HighlightedIndex = null;
            return;
        }

        HighlightedIndex = null;
        for (int i = 0; i < Resolutions.Count; i++)
        {
            Rectangle itemBounds = GetItemBounds(i);
            if (!itemBounds.Contains(input.MousePosition))
            {
                continue;
            }

            HighlightedIndex = i;

            if (input.LeftMousePressed)
            {
                SelectedResolution = Resolutions[i];
                IsExpanded = false;
                HighlightedIndex = null;
            }

            return;
        }

        if (input.LeftMousePressed && !GetExpandedInteractionBounds().Contains(input.MousePosition))
        {
            IsExpanded = false;
            HighlightedIndex = null;
        }
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        if (!string.IsNullOrWhiteSpace(Label))
        {
            SimpleTextRenderer.DrawString(spriteBatch, pixel, Label, new Vector2(Bounds.X, Bounds.Y - 25), 2, Color.White);
        }

        DrawHeader(spriteBatch, pixel);

        if (!IsExpanded)
        {
            return;
        }

        DrawDropdown(spriteBatch, pixel);
    }

    private void DrawHeader(SpriteBatch spriteBatch, Texture2D pixel)
    {
        Rectangle headerBounds = Bounds;
        Color headerBg = IsExpanded ? new Color(64, 78, 106) : new Color(46, 56, 76);
        Color border = IsExpanded ? new Color(255, 226, 122) : new Color(105, 121, 150);

        spriteBatch.Draw(pixel, new Rectangle(headerBounds.X + 3, headerBounds.Y + 4, headerBounds.Width, headerBounds.Height), new Color(4, 6, 10, 90));
        spriteBatch.Draw(pixel, headerBounds, headerBg);
        DrawHelper.DrawBorder(spriteBatch, pixel, headerBounds, border, IsExpanded ? 3 : 2);

        Rectangle textBounds = new(headerBounds.X + 12, headerBounds.Y, headerBounds.Width - 50, headerBounds.Height);
        string displayText = SelectedResolution?.ToString() ?? "Select";
        DrawFittedCentered(spriteBatch, pixel, displayText, textBounds, 2, Color.White);

        Rectangle arrowBounds = new(headerBounds.Right - 38, headerBounds.Y, 28, headerBounds.Height);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, IsExpanded ? "^" : "V", arrowBounds, 2, new Color(210, 220, 238));
    }

    private void DrawDropdown(SpriteBatch spriteBatch, Texture2D pixel)
    {
        Rectangle dropdownBounds = GetDropdownBounds();
        spriteBatch.Draw(pixel, new Rectangle(dropdownBounds.X + 4, dropdownBounds.Y + 5, dropdownBounds.Width, dropdownBounds.Height), new Color(3, 5, 9, 140));
        spriteBatch.Draw(pixel, dropdownBounds, new Color(31, 39, 57, 245));
        DrawHelper.DrawBorder(spriteBatch, pixel, dropdownBounds, new Color(120, 139, 172), 2);

        for (int i = 0; i < Resolutions.Count; i++)
        {
            Rectangle itemBounds = GetItemBounds(i);
            bool isHighlighted = HighlightedIndex == i;
            bool isSelected = SelectedResolution != null && SelectedResolution.Equals(Resolutions[i]);

            Color itemBg = isHighlighted
                ? new Color(74, 96, 132)
                : (isSelected ? new Color(55, 74, 102) : new Color(38, 48, 68));
            Color textColor = isHighlighted || isSelected ? new Color(255, 226, 122) : Color.White;

            spriteBatch.Draw(pixel, itemBounds, itemBg);
            spriteBatch.Draw(pixel, new Rectangle(itemBounds.X + 10, itemBounds.Bottom - 1, itemBounds.Width - 20, 1), new Color(85, 99, 126, 170));
            DrawFittedCentered(spriteBatch, pixel, Resolutions[i].ToString(), itemBounds, 2, textColor);
        }
    }

    private Rectangle GetDropdownBounds()
    {
        int height = GetDropdownHeight();
        int y = OpenUpwards ? Bounds.Y - height : Bounds.Bottom + 4;
        return new Rectangle(Bounds.X, y, Bounds.Width, height);
    }

    private Rectangle GetItemBounds(int index)
    {
        Rectangle dropdownBounds = GetDropdownBounds();
        int itemHeight = GetItemHeight();
        return new Rectangle(dropdownBounds.X, dropdownBounds.Y + (index * itemHeight), dropdownBounds.Width, itemHeight);
    }

    private Rectangle GetExpandedInteractionBounds()
    {
        Rectangle dropdownBounds = GetDropdownBounds();
        int left = Math.Min(Bounds.Left, dropdownBounds.Left);
        int top = Math.Min(Bounds.Top, dropdownBounds.Top);
        int right = Math.Max(Bounds.Right, dropdownBounds.Right);
        int bottom = Math.Max(Bounds.Bottom, dropdownBounds.Bottom);
        return new Rectangle(left, top, right - left, bottom - top);
    }

    private int GetDropdownHeight() => Resolutions.Count * GetItemHeight();

    private int GetItemHeight() => Math.Clamp(Bounds.Height - 8, 32, 40);

    private static void DrawFittedCentered(SpriteBatch spriteBatch, Texture2D pixel, string text, Rectangle bounds, int preferredScale, Color color)
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

        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, text, bounds, scale, color);
    }
}
