using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public sealed class PartyHudOverlay
{
    public static Rectangle GetPanelBounds(Viewport viewport, PartyManager party)
    {
        if (party.Members.Count == 0)
        {
            return Rectangle.Empty;
        }

        int scale = Math.Clamp(viewport.Height / 420, 1, 2);
        int rowHeight = SimpleTextRenderer.MeasureString("A", scale).Y + 6;
        int panelWidth = Math.Clamp((int)(viewport.Width * 0.22f), 180, 280);
        int panelHeight = 12 + (party.Members.Count * rowHeight) + 8;
        int x = Math.Max(8, viewport.Width - panelWidth - 12);
        int y = 12;
        return new Rectangle(x, y, panelWidth, panelHeight);
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport, PartyManager party)
    {
        if (party.Members.Count == 0)
        {
            return;
        }

        int scale = Math.Clamp(viewport.Height / 420, 1, 2);
        int rowHeight = SimpleTextRenderer.MeasureString("A", scale).Y + 6;
        Rectangle panel = GetPanelBounds(viewport, party);
        spriteBatch.Draw(pixel, panel, new Color(24, 28, 38, 210));
        DrawHelper.DrawBorder(spriteBatch, pixel, panel, new Color(90, 104, 130), 1);

        SimpleTextRenderer.DrawString(spriteBatch, pixel, "PARTY", new Vector2(panel.X + 10, panel.Y + 6), scale, new Color(255, 220, 80));

        int rowY = panel.Y + 8 + SimpleTextRenderer.MeasureString("PARTY", scale).Y;
        IReadOnlyList<PartyMember> members = party.Members;
        for (int i = 0; i < members.Count; i++)
        {
            PartyMember member = members[i];
            string line = PartyDisplayNames.FormatMemberListLabel(member);

            SimpleTextRenderer.DrawString(spriteBatch, pixel, line, new Vector2(panel.X + 10, rowY), scale, Color.White);
            rowY += rowHeight;
        }
    }
}
