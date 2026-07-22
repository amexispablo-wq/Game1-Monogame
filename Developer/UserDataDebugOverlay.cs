#nullable enable
using System.Collections.Generic;
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

        if (steamInput is not null)
        {
            AppendSteamInputDiagnostics(entries, steamInput);
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

    private static void AppendSteamInputDiagnostics(List<(string, Color)> entries, SteamInputManager steamInput)
    {
        entries.Add(("STEAM INPUT", Color.Cyan));

        bool initOk = steamInput.IsInitialized;
        entries.Add(($"Init: {steamInput.InitializationStatus}", initOk ? Color.LimeGreen : Color.OrangeRed));

        if (!initOk)
        {
            return;
        }

        int count = steamInput.ConnectedControllerCount;
        Color countColor = count > 0 ? Color.White : Color.Orange;
        string retrySuffix = count == 0 && steamInput.DetectionRetryCount > 0
            ? $" (retrying 1s, attempt {steamInput.DetectionRetryCount})"
            : string.Empty;
        entries.Add(($"Controllers: {count}{retrySuffix}", countColor));

        ulong setRaw = steamInput.GameplayActionSetRaw;
        entries.Add(($"Action Set: {steamInput.CurrentActionSetName} (0x{setRaw:X})",
            setRaw != 0 ? Color.White : Color.OrangeRed));

        string missing = steamInput.GetMissingActionSummary();
        if (missing.Length > 0)
        {
            entries.Add(($"MISSING HANDLES: {missing}", Color.OrangeRed));
        }

        entries.Add(($"Glyph Source: {steamInput.GlyphSource}", Color.White));
        entries.Add(($"Layout: {steamInput.ActiveLayoutLabel} | Refresh: {steamInput.LastLayoutRefreshUtc:HH:mm:ss}", Color.White));

        for (int slot = 0; slot < InputManager.MaxLocalPlayers; slot++)
        {
            ulong handle = steamInput.GetSlotHandleRaw(slot);
            if (handle == 0)
            {
                continue;
            }

            string type = steamInput.GetControllerLabel(slot);
            steamInput.TryGetAnalog(slot, SteamInputActionNames.Move, out float mx, out float my);
            string digital = steamInput.GetDigitalStateSummary(slot);
            entries.Add((
                $"Slot {slot}: 0x{handle:X} {type} | Move=({mx:0.00},{my:0.00})" +
                (digital.Length > 0 ? $" | {digital}" : string.Empty),
                Color.LightGreen));
        }

        if (SteamInputLog.Count > 0)
        {
            entries.Add(("RECENT LOG", Color.Cyan));
            for (int i = 0; i < SteamInputLog.Count; i++)
            {
                entries.Add((SteamInputLog.GetLine(i), Color.Gray));
            }
        }
    }
}
