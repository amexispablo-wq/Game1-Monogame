#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks.Developer.GameplayBenchmark;

public static class BenchmarkDebugOverlay
{
    public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport, BenchmarkRunner runner)
    {
        if (!DeveloperSettings.DeveloperMode || !runner.IsDebugVisible)
        {
            return;
        }

        Rectangle panel = new(viewport.Width - 340, 12, 328, 180);
        spriteBatch.Draw(pixel, panel, new Color(0, 0, 0, 180));
        DrawHelper.DrawBorder(spriteBatch, pixel, panel, Color.White, 1);

        Vector2 cursor = new(panel.X + 8, panel.Y + 8);
        SimpleTextRenderer.DrawString(spriteBatch, pixel, "BENCHMARK DEBUG (F11)", cursor, 1, Color.Cyan);
        cursor.Y += 14;
        SimpleTextRenderer.DrawString(spriteBatch, pixel, $"Mode: {runner.Mode}", cursor, 1, Color.White);
        cursor.Y += 12;
        SimpleTextRenderer.DrawString(spriteBatch, pixel, $"Scenario: {runner.CurrentScenario?.Name ?? runner.StatusMessage}", cursor, 1, Color.White);
        cursor.Y += 12;
        SimpleTextRenderer.DrawString(spriteBatch, pixel, $"Frame: {runner.Context?.SimulationFrame ?? 0}", cursor, 1, Color.White);
        cursor.Y += 12;
        SimpleTextRenderer.DrawString(spriteBatch, pixel, $"Seed: {runner.Context?.CurrentSeed?.ToString() ?? "-"}", cursor, 1, Color.White);
        cursor.Y += 12;
        SimpleTextRenderer.DrawString(spriteBatch, pixel, $"Assert: {runner.Context?.CurrentAssertion ?? "-"}", cursor, 1, Color.Gold);
        cursor.Y += 12;
        SimpleTextRenderer.DrawString(spriteBatch, pixel, $"RenderMs: {BenchmarkManager.LastRenderMs:0.##}", cursor, 1, Color.White);
        cursor.Y += 12;
        SimpleTextRenderer.DrawString(spriteBatch, pixel, $"Progress: {runner.CompletedCount}/{runner.TotalCount}", cursor, 1, Color.White);
    }
}
