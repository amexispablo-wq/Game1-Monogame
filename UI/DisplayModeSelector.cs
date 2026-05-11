#nullable enable
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game1_Monogame;

public enum DisplayMode
{
    Fullscreen,
    Windowed,
    BorderlessWindowed
}

public sealed class DisplayModeSelector
{
    public DisplayMode CurrentMode { get; set; } = DisplayMode.Windowed;
    public Rectangle Bounds { get; set; }
    public List<DisplayMode> AvailableModes { get; } = new()
    {
        DisplayMode.Fullscreen,
        DisplayMode.Windowed,
        DisplayMode.BorderlessWindowed
    };

    public bool IsExpanded { get; set; }
    public int? HighlightedIndex { get; set; }
    public int DropdownHeight { get; set; } = 100;

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
            for (int i = 0; i < AvailableModes.Count; i++)
            {
                int itemY = Bounds.Y + 40 + (i * 28);
                Rectangle itemBounds = new(Bounds.X, itemY, Bounds.Width, 28);

                if (itemBounds.Contains(input.MousePosition))
                {
                    HighlightedIndex = i;

                    if (input.LeftMousePressed)
                    {
                        CurrentMode = AvailableModes[i];
                        IsExpanded = false;
                        HighlightedIndex = null;
                    }
                }
            }

            if (input.LeftMousePressed && !new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, Bounds.Y + 40 + (AvailableModes.Count * 28)).Contains(input.MousePosition))
            {
                IsExpanded = false;
                HighlightedIndex = null;
            }
        }
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        // Draw header
        Color headerFill = IsExpanded ? new Color(60, 70, 90) : new Color(50, 60, 80);
        spriteBatch.Draw(pixel, new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, 40), headerFill);
        DrawHelper.DrawBorder(spriteBatch, pixel, new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, 40), new Color(80, 90, 110), 1);

        string displayText = CurrentMode switch
        {
            DisplayMode.Fullscreen => "Fullscreen",
            DisplayMode.Windowed => "Windowed",
            DisplayMode.BorderlessWindowed => "Borderless Windowed",
            _ => "Unknown"
        };

        SimpleTextRenderer.DrawString(spriteBatch, pixel, displayText, new Vector2(Bounds.X + 10, Bounds.Y + 10), 2, Color.White);

        // Draw dropdown arrow
        SimpleTextRenderer.DrawRight(spriteBatch, pixel, IsExpanded ? "▲" : "▼", new Vector2(Bounds.Right - 10, Bounds.Y + 10), 2, Color.LightGray);

        if (IsExpanded)
        {
            for (int i = 0; i < AvailableModes.Count; i++)
            {
                int itemY = Bounds.Y + 40 + (i * 28);
                Rectangle itemBounds = new(Bounds.X, itemY, Bounds.Width, 28);

                Color itemFill = HighlightedIndex == i ? new Color(74, 86, 110) : new Color(45, 52, 70);
                spriteBatch.Draw(pixel, itemBounds, itemFill);
                DrawHelper.DrawBorder(spriteBatch, pixel, itemBounds, new Color(70, 80, 100), 1);

                string modeText = AvailableModes[i] switch
                {
                    DisplayMode.Fullscreen => "Fullscreen",
                    DisplayMode.Windowed => "Windowed",
                    DisplayMode.BorderlessWindowed => "Borderless Windowed",
                    _ => "Unknown"
                };

                SimpleTextRenderer.DrawString(spriteBatch, pixel, modeText, new Vector2(itemBounds.X + 10, itemBounds.Y + 5), 2, Color.White);
            }
        }
    }

    public string GetModeDescription()
    {
        return CurrentMode switch
        {
            DisplayMode.Fullscreen => "Exclusive fullscreen mode",
            DisplayMode.Windowed => "Windowed mode with borders",
            DisplayMode.BorderlessWindowed => "Fullscreen windowed mode",
            _ => ""
        };
    }
}
