#nullable enable
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game1_Monogame;

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
        Rectangle headerBounds = new(Bounds.X, Bounds.Y, Bounds.Width, 40);

        if (input.LeftMousePressed && headerBounds.Contains(input.MousePosition))
        {
            IsExpanded = !IsExpanded;
            HighlightedIndex = null;
            return;
        }

        if (IsExpanded)
        {
            for (int i = 0; i < Resolutions.Count; i++)
            {
                int itemY = Bounds.Y + 40 + (i * 28);
                Rectangle itemBounds = new(Bounds.X, itemY, Bounds.Width, 28);

                if (itemBounds.Contains(input.MousePosition))
                {
                    HighlightedIndex = i;

                    if (input.LeftMousePressed)
                    {
                        SelectedResolution = Resolutions[i];
                        IsExpanded = false;
                        HighlightedIndex = null;
                    }
                }
            }

            if (input.LeftMousePressed && !new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, Bounds.Y + 40 + (Resolutions.Count * 28)).Contains(input.MousePosition))
            {
                IsExpanded = false;
                HighlightedIndex = null;
            }
        }
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        // Draw label
        SimpleTextRenderer.DrawString(spriteBatch, pixel, Label, new Vector2(Bounds.X, Bounds.Y - 25), 2, Color.White);

        // Draw header/button
        Rectangle headerBounds = new(Bounds.X, Bounds.Y, Bounds.Width, 40);
        Color headerBg = IsExpanded ? new Color(62, 71, 90) : new Color(52, 61, 80);
        spriteBatch.Draw(pixel, headerBounds, headerBg);
        DrawHelper.DrawBorder(spriteBatch, pixel, headerBounds, new Color(80, 90, 110), 2);

        // Draw selected resolution text
        string displayText = SelectedResolution?.ToString() ?? "Select...";
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, displayText, headerBounds, 2, Color.White);

        // Draw dropdown arrow indicator
        SimpleTextRenderer.DrawRight(spriteBatch, pixel, IsExpanded ? "▲" : "▼",
            new Vector2(headerBounds.Right - 10, headerBounds.Y + 10), 2, Color.LightGray);

        if (IsExpanded)
        {
            int maxItemsVisible = 6;
            int itemsToShow = System.Math.Min(Resolutions.Count, maxItemsVisible);
            int dropdownHeight = itemsToShow * 32;

            // Draw semi-transparent background for dropdown area
            Rectangle dropdownBg = new(Bounds.X, Bounds.Y + 40, Bounds.Width, dropdownHeight);
            spriteBatch.Draw(pixel, dropdownBg, new Color(35, 45, 65, 220));
            DrawHelper.DrawBorder(spriteBatch, pixel, dropdownBg, new Color(100, 110, 140), 2);

            for (int i = 0; i < itemsToShow; i++)
            {
                int itemY = Bounds.Y + 40 + (i * 32);
                Rectangle itemBounds = new(Bounds.X, itemY, Bounds.Width, 32);

                Color itemBg = HighlightedIndex == i ? new Color(80, 130, 200) : new Color(50, 60, 80);
                spriteBatch.Draw(pixel, itemBounds, itemBg);
                DrawHelper.DrawBorder(spriteBatch, pixel, itemBounds, new Color(70, 80, 110), 1);

                string itemText = Resolutions[i].ToString();
                SimpleTextRenderer.DrawCentered(spriteBatch, pixel, itemText, itemBounds, 2,
                    HighlightedIndex == i ? Color.Yellow : Color.White);
            }
        }
    }
}
