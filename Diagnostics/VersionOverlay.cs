#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

/// <summary>
/// Bottom-right build version block shown on the main menu and pause menu, so two players
/// can instantly verify they run the same build without opening any files.
/// </summary>
public static class VersionOverlay
{
    private static readonly Color TextColor = new(150, 162, 182);

    public static void DrawBottomRight(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport)
    {
        BuildInfo build = BuildInfo.Current;
        string[] lines =
        {
            $"Version {build.GameVersion}",
            $"Build {build.ShortBuildId}",
            $"Commit {build.GitCommit}"
        };

        const int scale = 1;
        const int margin = 10;
        int lineHeight = SimpleTextRenderer.MeasureString("A", scale).Y + 3;
        int y = viewport.Height - margin - (lines.Length * lineHeight);
        foreach (string line in lines)
        {
            Point size = SimpleTextRenderer.MeasureString(line, scale);
            var position = new Vector2(viewport.Width - margin - size.X, y);
            SimpleTextRenderer.DrawString(spriteBatch, pixel, line, position, scale, TextColor);
            y += lineHeight;
        }
    }
}
