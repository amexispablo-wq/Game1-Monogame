#nullable enable
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public static class UserDataDebugOverlay
{
    public static bool Visible { get; set; }

    public static void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Viewport viewport,
        InputManager? input = null)
    {
        if (!DeveloperSettings.DeveloperMode || !Visible)
        {
            return;
        }

        BuildInfo build = BuildInfo.Current;
        var entries = new List<(string Text, Color Color)>
        {
            ("BUILD (F3)", Color.Cyan),
            ($"Version: {build.GameVersion}", Color.White),
            ($"Build GUID: {build.BuildGuid}", Color.White),
            ($"Commit: {build.GitCommit} ({build.GitBranch})", Color.White),
            ($"Timestamp: {build.BuildTimestampUtc} [{build.Configuration}]", Color.White),
            ($"Session: {DiagnosticsLog.SessionId}", Color.White),
            ($"Log: {DiagnosticsLog.LogFilePath}", Color.Gray),
            ("USER DATA PATHS (F3)", Color.Cyan),
            ($"User Data Root: {UserDataPaths.Root}", Color.White),
            ($"Settings Path: {UserDataPaths.SettingsFile}", Color.White),
            ($"Levels Path: {UserDataPaths.UserLevels}", Color.White),
            ($"Replay Path: {UserDataPaths.Replays}", Color.White),
            ($"Ghost Path: {UserDataPaths.Ghosts}", Color.White),
            ($"Workshop Path: {UserDataPaths.Workshop}", Color.White),
            ($"Migration Status: {UserDataMigration.Status}", Color.Gold)
        };

        if (input is not null)
        {
            entries.Add(("INPUT", Color.Cyan));
            string backendLabel = input.ActiveInputBackend switch
            {
                ActiveInputBackend.Gamepad => "Gamepad",
                _ => "Keyboard"
            };
            entries.Add(($"Active Input Backend: {backendLabel}", Color.White));
            entries.Add(($"Any Gamepad: {(input.IsAnyGamepadConnected() ? "yes" : "no")}", Color.White));
            entries.Add((
                $"PartyLastUsed: {input.LastUsedPartyInputSource} id={input.LastUsedPartyControllerId}",
                Color.White));
        }

        const int margin = 12;
        const int lineHeight = 14;
        int panelWidth = System.Math.Max(320, viewport.Width - margin * 2);
        var panel = new Rectangle(margin, margin, panelWidth, entries.Count * lineHeight + 20);
        spriteBatch.Draw(pixel, panel, new Color(0, 0, 0, 210));
        DrawHelper.DrawBorder(spriteBatch, pixel, panel, Color.Cyan, 1);

        var cursor = new Vector2(panel.X + 8, panel.Y + 8);
        foreach ((string text, Color color) in entries)
        {
            SimpleTextRenderer.DrawString(spriteBatch, pixel, text, cursor, 1, color);
            cursor.Y += lineHeight;
        }
    }
}
