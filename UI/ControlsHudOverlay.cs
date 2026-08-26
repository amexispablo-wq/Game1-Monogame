using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public sealed class ControlsHudOverlay
{
    private static readonly Color PanelFill = new(24, 28, 38, 210);
    private static readonly Color PanelBorder = new(90, 104, 130);

    private static readonly (GameplayInputAction Action, string Label, GameColor? Tint)[] ColorRows =
    {
        (GameplayInputAction.Red, "RED", GameColor.Red),
        (GameplayInputAction.Green, "GREEN", GameColor.Green),
        (GameplayInputAction.Blue, "BLUE", GameColor.Blue)
    };

    private static readonly (GameplayInputAction Action, string Label, GameColor? Tint)[] ActionRows =
    {
        (GameplayInputAction.Jump, "JUMP", null),
        (GameplayInputAction.PullRope, "PULL ROPE", null),
        (GameplayInputAction.Respawn, "RESPAWN", null),
        (GameplayInputAction.RestartLevel, "RESTART LEVEL", null)
    };

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport, bool useGamepadBindings)
    {
        int scale = Math.Clamp(viewport.Height / 420, 1, 2);
        int rowHeight = SimpleTextRenderer.MeasureString("A", scale).Y + 6;
        int paddingX = 10;
        int paddingY = 8;
        int margin = 12;
        int gap = SimpleTextRenderer.MeasureString("  ", scale).X;

        DrawPanel(
            spriteBatch,
            pixel,
            ColorRows,
            scale,
            rowHeight,
            paddingX,
            paddingY,
            gap,
            useGamepadBindings,
            left: true,
            viewport,
            margin);

        DrawPanel(
            spriteBatch,
            pixel,
            ActionRows,
            scale,
            rowHeight,
            paddingX,
            paddingY,
            gap,
            useGamepadBindings,
            left: false,
            viewport,
            margin);
    }

    private static void DrawPanel(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        (GameplayInputAction Action, string Label, GameColor? Tint)[] rows,
        int scale,
        int rowHeight,
        int paddingX,
        int paddingY,
        int gap,
        bool useGamepadBindings,
        bool left,
        Viewport viewport,
        int margin)
    {
        var bindings = new string[rows.Length];
        int maxLabelWidth = 0;
        int maxKeyWidth = 0;
        for (int i = 0; i < rows.Length; i++)
        {
            bindings[i] = BindingDisplay.ForAction(rows[i].Action, useGamepadBindings);
            int labelWidth = SimpleTextRenderer.MeasureString(rows[i].Label, scale).X;
            int keyWidth = SimpleTextRenderer.MeasureString(bindings[i], scale).X;
            if (labelWidth > maxLabelWidth)
            {
                maxLabelWidth = labelWidth;
            }

            if (keyWidth > maxKeyWidth)
            {
                maxKeyWidth = keyWidth;
            }
        }

        int panelWidth = paddingX + maxLabelWidth + gap + maxKeyWidth + paddingX;
        int panelHeight = paddingY + (rows.Length * rowHeight) + 4;
        int x = left ? margin : viewport.Width - panelWidth - margin;
        int y = viewport.Height - panelHeight - margin;
        var panel = new Rectangle(x, y, panelWidth, panelHeight);

        spriteBatch.Draw(pixel, panel, PanelFill);
        DrawHelper.DrawBorder(spriteBatch, pixel, panel, PanelBorder, 1);

        int rowY = panel.Y + paddingY;
        int labelX = panel.X + paddingX;
        int keyX = labelX + maxLabelWidth + gap;
        for (int i = 0; i < rows.Length; i++)
        {
            Color labelColor = rows[i].Tint is GameColor tint
                ? ColorPaletteManager.GetGameColor(tint)
                : Color.White;

            SimpleTextRenderer.DrawString(
                spriteBatch,
                pixel,
                rows[i].Label,
                new Vector2(labelX, rowY),
                scale,
                labelColor);
            SimpleTextRenderer.DrawString(
                spriteBatch,
                pixel,
                bindings[i],
                new Vector2(keyX, rowY),
                scale,
                Color.White);
            rowY += rowHeight;
        }
    }
}
