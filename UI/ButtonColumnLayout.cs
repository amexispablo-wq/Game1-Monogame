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
    public Rectangle TitleBounds { get; private set; }
    public int TitleScale { get; private set; }
    public bool HasTitle { get; private set; }

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
        int horizontalPadding = 16,
        string? titleText = null)
    {
        var layout = new ButtonColumnLayout();

        if (buttonTexts.Length == 0)
        {
            layout.ButtonBounds = Array.Empty<Rectangle>();
            layout.TotalHeight = 0;
            return layout;
        }

        int maxButtonWidth = buttonWidth;

        for (int i = 0; i < buttonTexts.Length; i++)
        {
            Point measured = SimpleTextRenderer.MeasureString(buttonTexts[i], 4);
            int width = measured.X + (horizontalPadding * 2);
            maxButtonWidth = Math.Max(maxButtonWidth, width);
        }

        int totalHeight = (buttonTexts.Length * buttonHeight) + ((buttonTexts.Length - 1) * verticalGap);
        layout.TotalHeight = totalHeight;

        int contentTop = topMargin;
        if (!string.IsNullOrEmpty(titleText))
        {
            int titleTop = Math.Max(24, viewportHeight / 20);
            int titleScale = Math.Clamp(viewportHeight / 120, 3, 6);
            int titleHeight = SimpleTextRenderer.MeasureString(titleText, titleScale).Y;
            int titleGap = Math.Max(20, viewportHeight / 40);

            layout.HasTitle = true;
            layout.TitleScale = titleScale;
            layout.TitleBounds = new Rectangle(0, titleTop, viewportWidth, titleHeight);
            contentTop = titleTop + titleHeight + titleGap;
        }

        int bottomMargin = Math.Max(24, viewportHeight / 20);
        int availableHeight = viewportHeight - contentTop - bottomMargin;
        int startY = contentTop + (Math.Max(0, availableHeight - totalHeight) / 2);
        int startX = (viewportWidth - maxButtonWidth) / 2;

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
        int topMargin = 120,
        string? titleText = null)
    {
        return Create(buttonTexts, viewportWidth, viewportHeight, 150, buttonHeight, verticalGap, topMargin, 16, titleText);
    }
}
