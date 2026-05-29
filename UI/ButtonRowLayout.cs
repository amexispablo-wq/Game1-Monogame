using System;
using Microsoft.Xna.Framework;

namespace ColorBlocks;

public sealed class ButtonRowLayout
{
    public Rectangle[] ButtonBounds { get; private set; }
    public int TotalWidth { get; private set; }

    private ButtonRowLayout()
    {
        ButtonBounds = Array.Empty<Rectangle>();
    }

    public static ButtonRowLayout Create(string[] buttonTexts, int viewportWidth, int viewportHeight,
        int buttonHeight, int horizontalPadding = 16, int verticalPadding = 12, int buttonGap = 15, int bottomMargin = 25)
    {
        var layout = new ButtonRowLayout();

        if (buttonTexts.Length == 0)
        {
            layout.ButtonBounds = Array.Empty<Rectangle>();
            layout.TotalWidth = 0;
            return layout;
        }

        int[] buttonWidths = new int[buttonTexts.Length];
        int totalButtonWidth = 0;
        int minButtonWidth = 90;

        for (int i = 0; i < buttonTexts.Length; i++)
        {
            Point measured = SimpleTextRenderer.MeasureString(buttonTexts[i], 4);
            int width = measured.X + (horizontalPadding * 2);
            buttonWidths[i] = Math.Max(minButtonWidth, width);
            totalButtonWidth += buttonWidths[i];
        }

        int totalWidth = totalButtonWidth + ((buttonTexts.Length - 1) * buttonGap);
        int availableWidth = Math.Max(0, viewportWidth - 40);

        if (totalWidth > availableWidth)
        {
            int overflow = totalWidth - availableWidth;
            int reducePerButton = (int)Math.Ceiling(overflow / (double)buttonTexts.Length);

            totalButtonWidth = 0;
            for (int i = 0; i < buttonWidths.Length; i++)
            {
                buttonWidths[i] = Math.Max(minButtonWidth, buttonWidths[i] - reducePerButton);
                totalButtonWidth += buttonWidths[i];
            }

            totalWidth = totalButtonWidth + ((buttonTexts.Length - 1) * buttonGap);
        }

        layout.TotalWidth = totalWidth;
        int startX = (viewportWidth - totalWidth) / 2;
        int startY = viewportHeight - buttonHeight - bottomMargin;

        layout.ButtonBounds = new Rectangle[buttonTexts.Length];
        int currentX = startX;

        for (int i = 0; i < buttonTexts.Length; i++)
        {
            layout.ButtonBounds[i] = new Rectangle(currentX, startY, buttonWidths[i], buttonHeight);
            currentX += buttonWidths[i] + buttonGap;
        }

        return layout;
    }

    public static ButtonRowLayout CreateSingle(string buttonText, int viewportWidth, int viewportHeight,
        int buttonHeight, int horizontalPadding = 16, int bottomMargin = 25)
    {
        return Create(new[] { buttonText }, viewportWidth, viewportHeight, buttonHeight, horizontalPadding, 12, 0, bottomMargin);
    }
}
