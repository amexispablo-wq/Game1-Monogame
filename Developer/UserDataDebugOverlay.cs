#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public static class UserDataDebugOverlay
{
    public static bool Visible { get; set; }

    public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport, SteamInputManager? steamInput = null)
    {
        if (!DeveloperSettings.DeveloperMode || !Visible)
        {
            return;
        }

        const int margin = 12;
        const int lineHeight = 14;
        int lines = steamInput is null ? 9 : 14;
        int panelWidth = System.Math.Max(320, viewport.Width - margin * 2);
        var panel = new Rectangle(margin, margin, panelWidth, lines * lineHeight + 20);
        spriteBatch.Draw(pixel, panel, new Color(0, 0, 0, 210));
        DrawHelper.DrawBorder(spriteBatch, pixel, panel, Color.Cyan, 1);

        var cursor = new Vector2(panel.X + 8, panel.Y + 8);

        void Line(string text, Color color)
        {
            SimpleTextRenderer.DrawString(spriteBatch, pixel, text, cursor, 1, color);
            cursor.Y += lineHeight;
        }

        Line("USER DATA PATHS (F3)", Color.Cyan);
        Line($"User Data Root: {UserDataPaths.Root}", Color.White);
        Line($"Settings Path: {UserDataPaths.SettingsFile}", Color.White);
        Line($"Levels Path: {UserDataPaths.UserLevels}", Color.White);
        Line($"Replay Path: {UserDataPaths.Replays}", Color.White);
        Line($"Ghost Path: {UserDataPaths.Ghosts}", Color.White);
        Line($"Workshop Path: {UserDataPaths.Workshop}", Color.White);
        Line($"Migration Status: {UserDataMigration.Status}", Color.Gold);

        if (steamInput is not null)
        {
            Line("STEAM INPUT", Color.Cyan);
            Line($"Enabled: {(steamInput.IsInitialized ? "yes" : "no")}", Color.White);
            Line($"Action Set: {steamInput.CurrentActionSetName}", Color.White);
            Line($"Controllers: {steamInput.ConnectedControllerCount}", Color.White);
            Line($"Glyph Source: {steamInput.GlyphSource}", Color.White);
            Line($"Layout Refresh: {steamInput.LastLayoutRefreshUtc:HH:mm:ss}", Color.White);
        }
    }
}
