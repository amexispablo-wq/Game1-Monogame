#nullable enable
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using System;

namespace ColorBlocks.Developer.GameplayBenchmark;

public static class BenchmarkLevelFactory
{
    public static Level CreateFlatMovementArena(string name = "Benchmark Movement")
    {
        return new Level(
            new Vector2(200f, 300f),
            new[]
            {
                new Platform(new Rectangle(0, 420, 2400, 48), GameColor.Red)
            },
            System.Array.Empty<Goal>(),
            name: name)
        {
            AllPlayers = true,
            RegularRope = true
        };
    }

    public static Level CreateRopeArena(string name = "Benchmark Rope")
    {
        return Level.CreateRopeSandbox();
    }

    public static Level CreateReplayArena(string name = "Benchmark Replay")
    {
        return new Level(
            new Vector2(180f, 300f),
            new[]
            {
                new Platform(new Rectangle(0, 420, 1600, 48), GameColor.Red),
                new Platform(new Rectangle(420, 340, 220, 28), GameColor.Blue),
                new Platform(new Rectangle(820, 280, 220, 28), GameColor.Green)
            },
            new[] { new Goal(new Point(1400, 356)) },
            new[] { new CheckpointFlag(new Point(620, 356), 1) },
            name: name)
        {
            AllPlayers = true,
            ColoredRope = true
        };
    }

    public static Level CreateColoredRopeArena(string name = "Benchmark Colored Rope")
    {
        return new Level(
            new Vector2(260f, 320f),
            new[]
            {
                new Platform(new Rectangle(0, 420, 1800, 48), GameColor.Red),
                new Platform(new Rectangle(120, 340, 520, 28), GameColor.Red),
                new Platform(new Rectangle(760, 340, 520, 28), GameColor.Red),
                new Platform(new Rectangle(180, 392, 70, 16), GameColor.Red)
            },
            Array.Empty<Goal>(),
            name: name)
        {
            AllPlayers = true,
            ColoredRope = true
        };
    }

    public static Level CreateDiagonalBlockageArena(string name = "Benchmark Diagonal Blockage")
    {
        return new Level(
            new Vector2(140f, 300f),
            new[]
            {
                new Platform(new Rectangle(0, 420, 1600, 48), GameColor.Red),
                new Platform(new Rectangle(40, 340, 260, 28), GameColor.Green),
                new Platform(new Rectangle(340, 296, 420, 36), GameColor.Red),
                new Platform(new Rectangle(900, 260, 240, 28), GameColor.Blue)
            },
            Array.Empty<Goal>(),
            name: name)
        {
            AllPlayers = true,
            ColoredRope = true
        };
    }
}
