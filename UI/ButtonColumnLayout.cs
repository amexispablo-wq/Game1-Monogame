using System;
using Microsoft.Xna.Framework;

namespace ColorBlocks;

/// <summary>
/// Creates a vertical column layout for buttons with dynamic spacing and centering.
/// </summary>
public sealed class ButtonColumnLayout
{
    public Rectangle[] ButtonBounds { get; private set; }
    public int TotalHeight { get; private set; }

    private ButtonColumnLayout()
    {
        ButtonBounds = Array.Empty<Rectangle>();
    }

    public static ButtonColumnLayout Create(
        string[] buttonTexts,
        int viewportWidth,
        int viewportHeight,
        int buttonWidth = 200,
        int buttonHeight = 60,
        int verticalGap = 20,
        int topMargin = 120,
        int horizontalPadding = 16)
    {
        var layout = new ButtonColumnLayout();

        if (buttonTexts.Length == 0)
        {
            layout.ButtonBounds = Array.Empty<Rectangle>();
            layout.TotalHeight = 0;
            return layout;
        }

        // Calculate max button width to ensure all buttons have the same width
        int maxButtonWidth = buttonWidth;

        for (int i = 0; i < buttonTexts.Length; i++)
        {
            Point measured = SimpleTextRenderer.MeasureString(buttonTexts[i], 4);
            int width = measured.X + (horizontalPadding * 2);
            maxButtonWidth = Math.Max(maxButtonWidth, width);
        }

        // Calculate total height
        int totalHeight = (buttonTexts.Length * buttonHeight) + ((buttonTexts.Length - 1) * verticalGap);
        layout.TotalHeight = totalHeight;

        // Calculate vertical centering
        int availableHeight = viewportHeight - topMargin;
        int startY = topMargin + (Math.Max(0, availableHeight - totalHeight) / 2);

        // Calculate horizontal centering
        int startX = (viewportWidth - maxButtonWidth) / 2;

        // Create button bounds - ALL buttons use the same width (maxButtonWidth)
        layout.ButtonBounds = new Rectangle[buttonTexts.Length];
        int currentY = startY;

        for (int i = 0; i < buttonTexts.Length; i++)
        {
            layout.ButtonBounds[i] = new Rectangle(startX, currentY, maxButtonWidth, buttonHeight);
            currentY += buttonHeight + verticalGap;
        }

        return layout;
    }

    /// <summary>
    /// Creates a vertical column layout with automatic sizing based on content.
    /// </summary>
    public static ButtonColumnLayout CreateAuto(
        string[] buttonTexts,
        int viewportWidth,
        int viewportHeight,
        int buttonHeight = 60,
        int verticalGap = 20,
        int topMargin = 120)
    {
        return Create(buttonTexts, viewportWidth, viewportHeight, 150, buttonHeight, verticalGap, topMargin, 16);
    }
}
