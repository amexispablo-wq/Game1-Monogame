#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks.Developer.GameplayBenchmark;

public sealed class BenchmarkOverlay
{
    private enum MenuPage
    {
        Main,
        FuzzCounts,
        Settings,
        Results
    }

    private MenuPage _page = MenuPage.Main;
    private int _selectedIndex;

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport, BenchmarkRunner runner)
    {
        if (!runner.IsMenuOpen)
        {
            return;
        }

        Rectangle panel = new(40, 40, Math.Min(560, viewport.Width - 80), Math.Min(520, viewport.Height - 80));
        spriteBatch.Draw(pixel, panel, new Color(16, 20, 28, 240));
        DrawHelper.DrawBorder(spriteBatch, pixel, panel, new Color(96, 168, 255), 2);

        Vector2 cursor = new(panel.X + 16, panel.Y + 12);
        SimpleTextRenderer.DrawString(spriteBatch, pixel, "GAMEPLAY BENCHMARK", cursor, 2, Color.White);
        cursor.Y += 28;
        SimpleTextRenderer.DrawString(spriteBatch, pixel, runner.StatusMessage, cursor, 1, Color.Cyan);
        cursor.Y += 18;

        if (runner.Mode is BenchmarkRunMode.RunningAll or BenchmarkRunMode.RunningCategory or BenchmarkRunMode.RunningFuzz or BenchmarkRunMode.RunningReproduce)
        {
            DrawRunning(spriteBatch, pixel, ref cursor, runner);
            return;
        }

        switch (_page)
        {
            case MenuPage.Main:
                DrawMainMenu(spriteBatch, pixel, ref cursor, runner);
                break;
            case MenuPage.FuzzCounts:
                DrawFuzzMenu(spriteBatch, pixel, ref cursor, runner);
                break;
            case MenuPage.Settings:
                DrawSettings(spriteBatch, pixel, ref cursor);
                break;
            case MenuPage.Results:
                DrawResults(spriteBatch, pixel, ref cursor, runner);
                break;
        }
    }

    public void HandleInput(InputManager input, BenchmarkRunner runner)
    {
        if (!runner.IsMenuOpen || runner.Mode != BenchmarkRunMode.Menu)
        {
            return;
        }

        if (input.MenuMoveDownPressed)
        {
            _selectedIndex++;
        }

        if (input.MenuMoveUpPressed)
        {
            _selectedIndex--;
        }

        if (input.MenuCancelPressed)
        {
            if (_page != MenuPage.Main)
            {
                _page = MenuPage.Main;
                _selectedIndex = 0;
            }
            else
            {
                runner.CloseMenu();
            }
        }

        if (!input.MenuConfirmPressed)
        {
            return;
        }

        switch (_page)
        {
            case MenuPage.Main:
                ActivateMainSelection(runner);
                break;
            case MenuPage.FuzzCounts:
                ActivateFuzzSelection(runner);
                break;
            case MenuPage.Settings:
                AdjustSettings();
                break;
            case MenuPage.Results:
                _page = MenuPage.Main;
                _selectedIndex = 0;
                break;
        }
    }

    private void DrawMainMenu(SpriteBatch spriteBatch, Texture2D pixel, ref Vector2 cursor, BenchmarkRunner runner)
    {
        string[] items =
        {
            "Run All",
            "Movement",
            "Rope",
            "Replay",
            "Ghost",
            "Performance",
            "Fuzz Testing",
            "Settings",
            "Results",
            "Exit"
        };

        _selectedIndex = Wrap(_selectedIndex, items.Length);
        for (int i = 0; i < items.Length; i++)
        {
            Color color = i == _selectedIndex ? Color.Yellow : Color.White;
            SimpleTextRenderer.DrawString(spriteBatch, pixel, (i == _selectedIndex ? "> " : "  ") + items[i], cursor, 1, color);
            cursor.Y += 16;
        }
    }

    private void DrawFuzzMenu(SpriteBatch spriteBatch, Texture2D pixel, ref Vector2 cursor, BenchmarkRunner runner)
    {
        string[] items = { "Fuzz 100", "Fuzz 500", "Fuzz 1000", "Fuzz 5000", $"Reproduce Seed {BenchmarkSettings.Active.FuzzSeed}", "Back" };
        _selectedIndex = Wrap(_selectedIndex, items.Length);
        for (int i = 0; i < items.Length; i++)
        {
            Color color = i == _selectedIndex ? Color.Yellow : Color.White;
            SimpleTextRenderer.DrawString(spriteBatch, pixel, (i == _selectedIndex ? "> " : "  ") + items[i], cursor, 1, color);
            cursor.Y += 16;
        }
    }

    private void DrawSettings(SpriteBatch spriteBatch, Texture2D pixel, ref Vector2 cursor)
    {
        BenchmarkSettings settings = BenchmarkSettings.Active;
        string[] lines =
        {
            $"Movement Tolerance: {settings.MovementTolerance:0.##}",
            $"Replay Position Tol: {settings.ReplayPositionTolerance:0.##}",
            $"Ghost Position Tol: {settings.GhostPositionTolerance:0.##}",
            $"Max Sim Ms/Tick: {settings.MaxSimulationMsPerTick:0.##}",
            $"Fuzz Count Default: {settings.FuzzSimulationCount}",
            $"Fuzz Seed: {settings.FuzzSeed}",
            $"Fixed Fuzz Seed: {settings.UseFixedFuzzSeed}",
            "Save Settings",
            "Back"
        };

        _selectedIndex = Wrap(_selectedIndex, lines.Length);
        for (int i = 0; i < lines.Length; i++)
        {
            Color color = i == _selectedIndex ? Color.Yellow : Color.White;
            SimpleTextRenderer.DrawString(spriteBatch, pixel, (i == _selectedIndex ? "> " : "  ") + lines[i], cursor, 1, color);
            cursor.Y += 16;
        }
    }

    private void DrawResults(SpriteBatch spriteBatch, Texture2D pixel, ref Vector2 cursor, BenchmarkRunner runner)
    {
        BenchmarkReport? report = runner.LastReport;
        if (report is null)
        {
            SimpleTextRenderer.DrawString(spriteBatch, pixel, "No report yet.", cursor, 1, Color.White);
            return;
        }

        SimpleTextRenderer.DrawString(spriteBatch, pixel, $"PASS {report.PassCount}  WARN {report.WarningCount}  FAIL {report.FailCount}", cursor, 1, Color.White);
        cursor.Y += 18;
        int shown = 0;
        foreach (BenchmarkResult result in report.Results)
        {
            Color color = result.Verdict switch
            {
                BenchmarkVerdict.Pass => Color.LimeGreen,
                BenchmarkVerdict.Warning => Color.Gold,
                _ => Color.Red
            };
            SimpleTextRenderer.DrawString(spriteBatch, pixel, $"{result.Verdict} {result.ScenarioName}", cursor, 1, color);
            cursor.Y += 14;
            shown++;
            if (shown >= 18)
            {
                break;
            }
        }

        SimpleTextRenderer.DrawString(spriteBatch, pixel, "> Back", cursor, 1, _selectedIndex == 0 ? Color.Yellow : Color.White);
    }

    private void DrawRunning(SpriteBatch spriteBatch, Texture2D pixel, ref Vector2 cursor, BenchmarkRunner runner)
    {
        SimpleTextRenderer.DrawString(spriteBatch, pixel, $"Current: {runner.CurrentScenario?.Name ?? runner.StatusMessage}", cursor, 1, Color.White);
        cursor.Y += 16;
        SimpleTextRenderer.DrawString(spriteBatch, pixel, $"Progress {runner.CompletedCount}/{runner.TotalCount}", cursor, 1, Color.White);
        cursor.Y += 16;
        SimpleTextRenderer.DrawString(spriteBatch, pixel, $"Elapsed {runner.Elapsed.TotalSeconds:0.0}s", cursor, 1, Color.White);
        cursor.Y += 16;
        SimpleTextRenderer.DrawString(spriteBatch, pixel, $"ETA {runner.EstimatedRemaining.TotalSeconds:0.0}s", cursor, 1, Color.White);
        cursor.Y += 16;
        if (runner.Context?.CurrentSeed is int seed)
        {
            SimpleTextRenderer.DrawString(spriteBatch, pixel, $"Seed {seed}", cursor, 1, Color.Cyan);
            cursor.Y += 16;
        }

        if (runner.Context?.CurrentAssertion is string assertion)
        {
            SimpleTextRenderer.DrawString(spriteBatch, pixel, $"Assert: {assertion}", cursor, 1, Color.Gold);
        }
    }

    private void ActivateMainSelection(BenchmarkRunner runner)
    {
        switch (_selectedIndex)
        {
            case 0: runner.StartAll(); break;
            case 1: runner.StartCategory(BenchmarkCategory.Movement); break;
            case 2: runner.StartCategory(BenchmarkCategory.Rope); break;
            case 3: runner.StartCategory(BenchmarkCategory.Replay); break;
            case 4: runner.StartCategory(BenchmarkCategory.Ghost); break;
            case 5: runner.StartCategory(BenchmarkCategory.Performance); break;
            case 6: _page = MenuPage.FuzzCounts; _selectedIndex = 0; break;
            case 7: _page = MenuPage.Settings; _selectedIndex = 0; break;
            case 8: _page = MenuPage.Results; _selectedIndex = 0; break;
            case 9: runner.CloseMenu(); break;
        }
    }

    private void ActivateFuzzSelection(BenchmarkRunner runner)
    {
        switch (_selectedIndex)
        {
            case 0: runner.StartFuzz(100); break;
            case 1: runner.StartFuzz(500); break;
            case 2: runner.StartFuzz(1000); break;
            case 3: runner.StartFuzz(5000); break;
            case 4: runner.StartReproduceSeed(BenchmarkSettings.Active.FuzzSeed); break;
            case 5: _page = MenuPage.Main; _selectedIndex = 0; break;
        }
    }

    private void AdjustSettings()
    {
        BenchmarkSettings settings = BenchmarkSettings.Active;
        switch (_selectedIndex)
        {
            case 0: settings.MovementTolerance += 0.5f; break;
            case 1: settings.ReplayPositionTolerance += 0.25f; break;
            case 2: settings.GhostPositionTolerance += 0.25f; break;
            case 3: settings.MaxSimulationMsPerTick += 0.5d; break;
            case 4: settings.FuzzSimulationCount += 50; break;
            case 5: settings.FuzzSeed += 1; break;
            case 6: settings.UseFixedFuzzSeed = !settings.UseFixedFuzzSeed; break;
            case 7: settings.Save(); break;
            case 8: _page = MenuPage.Main; _selectedIndex = 0; break;
        }
    }

    private static int Wrap(int index, int count) => count <= 0 ? 0 : ((index % count) + count) % count;
}
