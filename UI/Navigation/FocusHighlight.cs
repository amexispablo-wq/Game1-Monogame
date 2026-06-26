using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public static class FocusHighlight
{
    private static readonly Color GlowColor = new(96, 168, 255, 42);
    private static readonly Color BorderColor = new(168, 214, 255);
    private static readonly Color InnerBorderColor = new(220, 238, 255, 180);

    public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, double totalSeconds)
    {
        float pulse = 0.5f + (MathF.Sin((float)totalSeconds * 4.5f) * 0.5f);
        int padding = 3 + (int)MathF.Round(pulse);
        float scale = 1f + (pulse * 0.012f);

        int scaledWidth = (int)MathF.Round(bounds.Width * scale);
        int scaledHeight = (int)MathF.Round(bounds.Height * scale);
        int offsetX = (scaledWidth - bounds.Width) / 2;
        int offsetY = (scaledHeight - bounds.Height) / 2;
        Rectangle scaled = new(bounds.X - offsetX, bounds.Y - offsetY, scaledWidth, scaledHeight);

        Rectangle outer = new(scaled.X - padding, scaled.Y - padding, scaled.Width + padding * 2, scaled.Height + padding * 2);
        Rectangle glow = outer;
        glow.Inflate(2, 2);

        byte glowAlpha = (byte)Math.Clamp(34 + (pulse * 36f), 34, 78);
        spriteBatch.Draw(pixel, glow, GlowColor * (glowAlpha / 255f));
        DrawHelper.DrawBorder(spriteBatch, pixel, outer, BorderColor * (0.72f + pulse * 0.28f), 2);
        DrawHelper.DrawBorder(spriteBatch, pixel, scaled, InnerBorderColor * (0.55f + pulse * 0.25f), 1);
    }
}
