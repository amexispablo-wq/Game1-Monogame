#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace ColorBlocks.Developer.GameplayBenchmark.FuzzTesting;

public static class FuzzGenerator
{
    private const int WorldWidth = 1400;
    private const int WorldHeight = 720;
    private const int FloorY = 560;
    private const int FloorHeight = 48;
    private const float PlayerHeight = 40f;
    private const float SpawnClearance = 8f;

    public static FuzzScenario Create(int seed)
    {
        BenchmarkRandom random = new(seed);
        int playerCount = random.NextInt(1, PartyManager.MaxMembers + 1);
        bool coloredRope = random.NextBool(0.45f);
        bool regularRope = !coloredRope || random.NextBool();
        bool lava = random.NextBool(0.15f);

        float spawnX = random.NextFloat(160f, WorldWidth - (playerCount * 60f) - 120f);
        float spawnY = FloorY - PlayerHeight - SpawnClearance;

        LevelData data = new()
        {
            Name = $"Fuzz_{seed}",
            PlayerSpawn = new Vector2Data { X = spawnX, Y = spawnY },
            AllPlayers = true,
            ColoredRope = coloredRope,
            RegularRope = regularRope,
            LavaRise = lava,
            LavaLine = lava
                ? new LavaLineData
                {
                    SurfaceY = FloorY + FloorHeight + random.NextInt(40, 120),
                    RiseSpeed = random.NextFloat(12f, 40f)
                }
                : null
        };

        data.Platforms.Add(new PlatformData
        {
            X = 0,
            Y = FloorY,
            Width = WorldWidth,
            Height = FloorHeight,
            Color = GameColor.Red
        });

        data.Platforms.Add(new PlatformData
        {
            X = -32,
            Y = 180,
            Width = 32,
            Height = 460,
            Color = GameColor.Red
        });

        data.Platforms.Add(new PlatformData
        {
            X = WorldWidth,
            Y = 180,
            Width = 32,
            Height = 460,
            Color = GameColor.Red
        });

        int platformCount = random.NextInt(1, 5);
        for (int i = 0; i < platformCount; i++)
        {
            int width = random.NextInt(100, 320);
            int x = random.NextInt(40, WorldWidth - width - 40);
            int y = random.NextInt(300, FloorY - 80);
            data.Platforms.Add(new PlatformData
            {
                X = x,
                Y = y,
                Width = width,
                Height = random.NextInt(20, 36),
                Color = random.Pick(new[] { GameColor.Red, GameColor.Green, GameColor.Blue })
            });
        }

        if (random.NextBool(0.65f))
        {
            int goalMin = (int)spawnX + 120;
            int goalMax = WorldWidth - 80;
            data.Goals.Add(new GoalData
            {
                X = Range(random, goalMin, goalMax),
                Y = (int)spawnY
            });
        }

        if (random.NextBool(0.45f))
        {
            int checkpointMin = (int)spawnX + 60;
            int checkpointMax = WorldWidth - 160;
            data.CheckpointFlags.Add(new CheckpointFlagData
            {
                Id = 1,
                X = Range(random, checkpointMin, checkpointMax),
                Y = (int)spawnY
            });
        }

        if (random.NextBool(0.3f))
        {
            int padWidth = LaunchPad.DefaultWidth;
            int padMin = 120;
            int padMax = WorldWidth - padWidth - 120;
            data.LaunchPads.Add(new LaunchPadData
            {
                X = Range(random, padMin, padMax),
                Y = FloorY - LaunchPad.DefaultHeight,
                Width = padWidth,
                Height = LaunchPad.DefaultHeight,
                RotationDegrees = random.NextFloat(-25f, 25f)
            });
        }

        Level level = Level.FromData(data);
        EnsureSpawnOnFloor(level, data.PlayerSpawn, playerCount);

        List<FuzzInputFrame> frames = BuildInputFrames(random);
        int maxTicks = frames.Count > 0 ? frames[^1].Tick + random.NextInt(20, 60) : 240;

        return new FuzzScenario
        {
            Seed = seed,
            Level = level,
            PlayerCount = playerCount,
            RopeMode = coloredRope ? RopeGameplayMode.ColoredPhysics : RopeGameplayMode.Neutral,
            LavaRiseEnabled = lava,
            ReplayEnabled = random.NextBool(0.35f),
            GhostEnabled = false,
            InputFrames = frames,
            MaxTicks = Math.Clamp(maxTicks, 150, 360)
        };
    }

    private static List<FuzzInputFrame> BuildInputFrames(BenchmarkRandom random)
    {
        List<FuzzInputFrame> frames = new();
        int tick = 0;
        int maxTicks = random.NextInt(150, 301);
        while (tick < maxTicks)
        {
            int burstLength = random.NextInt(10, 28);
            float horizontal = random.NextBool(0.45f) ? (random.NextBool() ? 1f : -1f) : 0f;
            bool jump = random.NextBool(0.12f);
            bool pull = random.NextBool(0.14f);
            bool fastFall = random.NextBool(0.08f);
            bool respawn = random.NextBool(0.03f);

            frames.Add(new FuzzInputFrame
            {
                Tick = tick,
                Horizontal = horizontal,
                Jump = jump,
                Pull = pull,
                FastFall = fastFall,
                Respawn = respawn
            });

            tick += burstLength;
        }

        frames.Add(new FuzzInputFrame
        {
            Tick = tick,
            Horizontal = 0f,
            Jump = false,
            Pull = false,
            FastFall = false,
            Respawn = false
        });

        return frames;
    }

    private static void EnsureSpawnOnFloor(Level level, Vector2Data spawn, int playerCount)
    {
        float maxSpawnX = WorldWidth - (playerCount * 50f) - 40f;
        spawn.X = Math.Clamp(spawn.X, 120f, maxSpawnX);
        spawn.Y = FloorY - PlayerHeight - SpawnClearance;

        Platform? floor = null;
        foreach (Platform platform in level.Platforms)
        {
            if (platform.Bounds.Y >= FloorY - 4)
            {
                floor = platform;
                break;
            }
        }

        if (floor is null)
        {
            return;
        }

        float left = floor.Bounds.Left + 24f;
        float right = floor.Bounds.Right - (playerCount * 50f) - 24f;
        spawn.X = Math.Clamp(spawn.X, left, Math.Max(left, right));
        level.PlayerStart = new Vector2(spawn.X, spawn.Y);
    }

    private static int Range(BenchmarkRandom random, int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            return minInclusive;
        }

        return random.NextInt(minInclusive, maxExclusive);
    }
}
