using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public sealed class ControlsHudOverlay
{
    private static readonly (GameplayInputAction Action, string Label)[] Rows =
    {
        (GameplayInputAction.Red, "RED"),
        (GameplayInputAction.Green, "GREEN"),
        (GameplayInputAction.Blue, "BLUE"),
        (GameplayInputAction.Jump, "JUMP"),
        (GameplayInputAction.PullRope, "PULL ROPE"),
        (GameplayInputAction.Respawn, "RESPAWN"),
        (GameplayInputAction.RestartLevel, "RESTART LEVEL")
    };

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport, bool useGamepadBindings)
    {
        int scale = Math.Clamp(viewport.Height / 420, 1, 2);
        int rowHeight = SimpleTextRenderer.MeasureString("A", scale).Y + 6;
        int paddingX = 10;
        int paddingY = 8;

        int maxWidth = 0;
        var lines = new string[Rows.Length];
        for (int i = 0; i < Rows.Length; i++)
        {
            string binding = BindingDisplay.ForAction(Rows[i].Action, useGamepadBindings);
            lines[i] = $"{Rows[i].Label}  {binding}";
            Point size = SimpleTextRenderer.MeasureString(lines[i], scale);
            if (size.X > maxWidth)
            {
                maxWidth = size.X;
            }
        }

        int panelWidth = maxWidth + (paddingX * 2);
        int panelHeight = paddingY + (Rows.Length * rowHeight) + 4;
        int x = 12;
        int y = Math.Max(8, (int)(viewport.Height * 0.035f));
        var panel = new Rectangle(x, y, panelWidth, panelHeight);

        spriteBatch.Draw(pixel, panel, new Color(24, 28, 38, 210));
        DrawHelper.DrawBorder(spriteBatch, pixel, panel, new Color(90, 104, 130), 1);

        int rowY = panel.Y + paddingY;
        for (int i = 0; i < lines.Length; i++)
        {
            SimpleTextRenderer.DrawString(
                spriteBatch,
                pixel,
                lines[i],
                new Vector2(panel.X + paddingX, rowY),
                scale,
                Color.White);
            rowY += rowHeight;
        }
    }
}
