#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks.Developer.GameplayBenchmark;

public static class BenchmarkManager
{
    public static BenchmarkRunner Runner { get; } = new();
    public static double LastRenderMs { get; set; }

    public static void Update(GameTime gameTime, InputManager input)
    {
        if (!DeveloperSettings.DeveloperMode)
        {
            Runner.CloseMenu();
            return;
        }

        if (input.BenchmarkTogglePressed)
        {
            Runner.ToggleMenu();
        }

        if (input.BenchmarkDebugTogglePressed)
        {
            Runner.ToggleDebug();
        }

        Runner.Update(Math.Max(4d, gameTime.ElapsedGameTime.TotalMilliseconds));
    }
}
