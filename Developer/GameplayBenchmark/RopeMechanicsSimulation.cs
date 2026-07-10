#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;

namespace ColorBlocks.Developer.GameplayBenchmark;

public sealed class RopeMechanicResult
{
    public required string MechanicId { get; init; }
    public required string MechanicName { get; init; }
    public BenchmarkStatistics Statistics { get; init; } = new();
    public List<BenchmarkAssertion> Assertions { get; init; } = new();
}

public static class RopeMechanicsSimulation
{
    public static IReadOnlyList<RopeMechanicResult> RunAll(
        BenchmarkContext context,
        RopeGameplayMode ropeMode,
        Level level,
        int playerCount = 2)
    {
        return new[]
        {
            RunSlack(context, ropeMode, level, playerCount),
            RunStretch(context, ropeMode, level, playerCount),
            RunCompression(context, ropeMode, level, playerCount),
            RunPull(context, ropeMode, level, playerCount),
            RunSwing(context, ropeMode, level, playerCount),
            RunVerticalHang(context, ropeMode, level, playerCount),
            RunHorizontalCoMove(context, ropeMode, level, playerCount),
            RunRepeatedJumping(context, ropeMode, level, playerCount),
            RunRepeatedPulling(context, ropeMode, level, playerCount),
            RunRecovery(context, ropeMode, level, playerCount),
            RunColoredPlatformCollision(context, ropeMode, level, playerCount),
            RunDiagonalPlatformBlockage(context, ropeMode, level, playerCount),
            RunPullStability(context, ropeMode, level, playerCount),
            RunSegmentPenetrationGuard(context, ropeMode, level, playerCount)
        };
    }

    private static RopeMechanicResult RunSlack(BenchmarkContext context, RopeGameplayMode mode, Level level, int playerCount)
    {
        using BenchmarkHarness harness = CreateHarness(context, mode, level, playerCount);
        harness.ApplyUniformInput(PlayerInputState.Empty);
        harness.RunTicks(90);
        Rope rope = harness.Simulation.Ropes[0];

        RopeMechanicResult result = CreateResult("slack", "Slack");
        result.Statistics.Set("slack", rope.SlackAmount);
        result.Statistics.Set("tension", rope.LastTension);
        result.Assertions.Add(rope.SlackAmount >= 0f
            ? BenchmarkAssertion.Pass("rope.slack", "Slack non-negative.", rope.SlackAmount)
            : BenchmarkAssertion.Fail("rope.slack", "Negative slack.", rope.SlackAmount));
        AddPhysics(result, harness, level);
        return result;
    }

    private static RopeMechanicResult RunStretch(BenchmarkContext context, RopeGameplayMode mode, Level level, int playerCount)
    {
        using BenchmarkHarness harness = CreateHarness(context, mode, level, playerCount);
        SetOppositeHorizontal(harness, 1f);
        RopeMetrics metrics = SampleRope(harness, 300);
        RopeMechanicResult result = CreateResult("stretch", "Maximum Stretch");
        WriteRopeMetrics(result, metrics);
        result.Assertions.Add(metrics.MaxStretch >= 0f
            ? BenchmarkAssertion.Pass("rope.stretch", "Stretch measured.", metrics.MaxStretch)
            : BenchmarkAssertion.Fail("rope.stretch", "Stretch invalid.", metrics.MaxStretch));
        result.Assertions.Add(metrics.MaxTension < 50f
            ? BenchmarkAssertion.Pass("rope.tension", "Tension stayed bounded.", metrics.MaxTension)
            : BenchmarkAssertion.Fail("rope.tension", "Tension exploded.", metrics.MaxTension));
        AddPhysics(result, harness, level);
        return result;
    }

    private static RopeMechanicResult RunCompression(BenchmarkContext context, RopeGameplayMode mode, Level level, int playerCount)
    {
        using BenchmarkHarness harness = CreateHarness(context, mode, level, playerCount);
        SetOppositeHorizontal(harness, -1f);
        RopeMetrics metrics = SampleRope(harness, 180);
        RopeMechanicResult result = CreateResult("compression", "Compression");
        WriteRopeMetrics(result, metrics);
        result.Assertions.Add(BenchmarkAssertion.Pass("rope.compression", "Compression phase simulated.", metrics.MinSlack));
        AddPhysics(result, harness, level);
        return result;
    }

    private static RopeMechanicResult RunPull(BenchmarkContext context, RopeGameplayMode mode, Level level, int playerCount)
    {
        using BenchmarkHarness harness = CreateHarness(context, mode, level, playerCount);
        float start = harness.Simulation.Ropes[0].TargetRestLength;
        harness.ApplyUniformInput(new PlayerInputState(0f, false, false, false, true, null));
        RopeMetrics metrics = SampleRope(harness, 180);
        float end = harness.Simulation.Ropes[0].TargetRestLength;

        RopeMechanicResult result = CreateResult("pull", "Pull Rope");
        result.Statistics.Set("target_delta", start - end);
        WriteRopeMetrics(result, metrics);
        result.Assertions.Add(end < start
            ? BenchmarkAssertion.Pass("rope.pull", "Pull shortened target length.", start - end)
            : BenchmarkAssertion.Fail("rope.pull", "Pull did not shorten rope.", end, start));
        AddPhysics(result, harness, level);
        return result;
    }

    private static RopeMechanicResult RunSwing(BenchmarkContext context, RopeGameplayMode mode, Level level, int playerCount)
    {
        using BenchmarkHarness harness = CreateHarness(context, mode, level, playerCount);
        harness.Input.SetInput(harness.Simulation.Players[0].NetworkId, PlayerInputState.Empty);
        harness.Input.SetInput(harness.Simulation.Players[1].NetworkId, new PlayerInputState(1f, true, false, false, false, null));
        RopeMetrics metrics = SampleRope(harness, 240);

        RopeMechanicResult result = CreateResult("swing", "Swing");
        WriteRopeMetrics(result, metrics);
        result.Assertions.Add(metrics.MaxTension > 0f || metrics.MaxOscillation > 0f
            ? BenchmarkAssertion.Pass("rope.swing", "Swing produced rope dynamics.", metrics.MaxTension)
            : BenchmarkAssertion.Warn("rope.swing", "Swing dynamics were weak.", metrics.MaxTension));
        AddPhysics(result, harness, level);
        return result;
    }

    private static RopeMechanicResult RunVerticalHang(BenchmarkContext context, RopeGameplayMode mode, Level level, int playerCount)
    {
        using BenchmarkHarness harness = CreateHarness(context, mode, level, playerCount);
        harness.Input.SetInput(harness.Simulation.Players[0].NetworkId, PlayerInputState.Empty);
        harness.Input.SetInput(harness.Simulation.Players[1].NetworkId, new PlayerInputState(0f, true, false, false, false, null));
        RopeMetrics metrics = SampleRope(harness, 200);

        RopeMechanicResult result = CreateResult("vertical_hang", "Vertical Hanging");
        WriteRopeMetrics(result, metrics);
        float verticalDelta = MathF.Abs(harness.Simulation.Players[1].Position.Y - harness.Simulation.Players[0].Position.Y);
        result.Statistics.Set("vertical_delta", verticalDelta);
        result.Assertions.Add(verticalDelta > 4f
            ? BenchmarkAssertion.Pass("rope.vertical", "Vertical separation observed.", verticalDelta)
            : BenchmarkAssertion.Warn("rope.vertical", "Vertical hang weak.", verticalDelta));
        AddPhysics(result, harness, level);
        return result;
    }

    private static RopeMechanicResult RunHorizontalCoMove(BenchmarkContext context, RopeGameplayMode mode, Level level, int playerCount)
    {
        using BenchmarkHarness harness = CreateHarness(context, mode, level, playerCount);
        Dictionary<int, Vector2> starts = CapturePositions(harness);
        harness.ApplyUniformInput(new PlayerInputState(1f, false, false, false, false, null));
        harness.RunTicks(180);

        RopeMechanicResult result = CreateResult("horizontal", "Horizontal Movement");
        float maxVelDelta = 0f;
        float maxDispDelta = 0f;
        Player reference = harness.Simulation.Players[0];
        Vector2 refDisp = reference.Position - starts[reference.NetworkId];
        foreach (Player player in harness.Simulation.Players)
        {
            maxVelDelta = MathF.Max(maxVelDelta, MathF.Abs(reference.Velocity.X - player.Velocity.X));
            maxDispDelta = MathF.Max(maxDispDelta, Vector2.Distance(refDisp, player.Position - starts[player.NetworkId]));
        }

        result.Statistics.Set("velocity_delta", maxVelDelta);
        result.Statistics.Set("displacement_delta", maxDispDelta);
        float tolerance = context.Settings.MovementTolerance;
        result.Assertions.Add(maxVelDelta <= tolerance && maxDispDelta <= tolerance
            ? BenchmarkAssertion.Pass("rope.comove", "Co-moving players stayed aligned.", maxVelDelta)
            : BenchmarkAssertion.Fail("rope.comove", "Co-moving players diverged.", maxDispDelta, 0f, tolerance));
        AddPhysics(result, harness, level);
        return result;
    }

    private static RopeMechanicResult RunRepeatedJumping(BenchmarkContext context, RopeGameplayMode mode, Level level, int playerCount)
    {
        using BenchmarkHarness harness = CreateHarness(context, mode, level, playerCount);
        RopeMetrics metrics = new();
        for (int tick = 0; tick < 240; tick++)
        {
            bool jump = tick % 45 == 0;
            harness.ApplyUniformInput(new PlayerInputState(0f, jump, false, false, false, null));
            harness.Simulation.Advance(harness.Simulation.TickRate.FixedDeltaSeconds, harness.Input);
            metrics.Observe(harness.Simulation.Ropes[0]);
        }

        metrics.Finish();
        RopeMechanicResult result = CreateResult("repeat_jump", "Repeated Jumping");
        WriteRopeMetrics(result, metrics);
        result.Assertions.Add(BenchmarkAssertion.Pass("rope.repeat_jump", "Repeated jumping completed.", metrics.SampleCount));
        AddPhysics(result, harness, level);
        return result;
    }

    private static RopeMechanicResult RunRepeatedPulling(BenchmarkContext context, RopeGameplayMode mode, Level level, int playerCount)
    {
        using BenchmarkHarness harness = CreateHarness(context, mode, level, playerCount);
        float start = harness.Simulation.Ropes[0].TargetRestLength;
        RopeMetrics metrics = new();
        for (int tick = 0; tick < 240; tick++)
        {
            bool pull = (tick / 30) % 2 == 0;
            harness.ApplyUniformInput(new PlayerInputState(0f, false, false, false, pull, null));
            harness.Simulation.Advance(harness.Simulation.TickRate.FixedDeltaSeconds, harness.Input);
            metrics.Observe(harness.Simulation.Ropes[0]);
        }

        metrics.Finish();
        RopeMechanicResult result = CreateResult("repeat_pull", "Repeated Pulling");
        WriteRopeMetrics(result, metrics);
        result.Statistics.Set("target_delta", start - harness.Simulation.Ropes[0].TargetRestLength);
        result.Assertions.Add(harness.Simulation.Ropes[0].TargetRestLength < start
            ? BenchmarkAssertion.Pass("rope.repeat_pull", "Repeated pulling shortened rope.", start - harness.Simulation.Ropes[0].TargetRestLength)
            : BenchmarkAssertion.Warn("rope.repeat_pull", "Repeated pulling had little effect."));
        AddPhysics(result, harness, level);
        return result;
    }

    private static RopeMechanicResult RunRecovery(BenchmarkContext context, RopeGameplayMode mode, Level level, int playerCount)
    {
        using BenchmarkHarness harness = CreateHarness(context, mode, level, playerCount);
        SetOppositeHorizontal(harness, 1f);
        SampleRope(harness, 120);
        harness.ApplyUniformInput(PlayerInputState.Empty);
        int recoveryTicks = 0;
        float peakTension = harness.Simulation.Ropes[0].LastTension;
        for (; recoveryTicks < 180; recoveryTicks++)
        {
            harness.Simulation.Advance(harness.Simulation.TickRate.FixedDeltaSeconds, harness.Input);
            if (harness.Simulation.Ropes[0].LastTension <= peakTension * 0.35f)
            {
                break;
            }
        }

        RopeMechanicResult result = CreateResult("recovery", "Recovery Time");
        result.Statistics.Set("recovery_ticks", recoveryTicks);
        result.Assertions.Add(recoveryTicks < 180
            ? BenchmarkAssertion.Pass("rope.recovery", "Rope tension recovered.", recoveryTicks)
            : BenchmarkAssertion.Warn("rope.recovery", "Rope recovery slow.", recoveryTicks));
        AddPhysics(result, harness, level);
        return result;
    }

    private static RopeMechanicResult RunColoredPlatformCollision(BenchmarkContext context, RopeGameplayMode mode, Level level, int playerCount)
    {
        RopeMechanicResult result = CreateResult("colored_collision", "Colored Platform Collision");
        if (mode != RopeGameplayMode.ColoredPhysics)
        {
            result.Assertions.Add(BenchmarkAssertion.Pass("rope.colored_collision", "Skipped for regular rope."));
            return result;
        }

        Level collisionLevel = BenchmarkLevelFactory.CreateColoredRopeArena("Colored Rope Collision");
        using BenchmarkHarness harness = CreateHarness(context, mode, collisionLevel, playerCount);
        SetOppositeHorizontal(harness, 1f);
        int maxHits = 0;
        harness.RunTicks(360, _ => maxHits = Math.Max(maxHits, harness.Simulation.Ropes[0].LastCollisionCount));

        result.Statistics.Set("collision_hits", maxHits);
        result.Assertions.Add(maxHits > 0
            ? BenchmarkAssertion.Pass("rope.colored_collision", "Colored rope collided with matching platforms.", maxHits)
            : BenchmarkAssertion.Warn("rope.colored_collision", "No colored rope collisions detected.", maxHits));
        AddPhysics(result, harness, collisionLevel);
        return result;
    }

    private static RopeMechanicResult RunDiagonalPlatformBlockage(
        BenchmarkContext context,
        RopeGameplayMode mode,
        Level level,
        int playerCount)
    {
        RopeMechanicResult result = CreateResult("diagonal_block", "Diagonal Platform Blockage");
        if (mode != RopeGameplayMode.ColoredPhysics)
        {
            result.Assertions.Add(BenchmarkAssertion.Pass("rope.diagonal_block", "Skipped for regular rope."));
            return result;
        }

        Level blockageLevel = BenchmarkLevelFactory.CreateDiagonalBlockageArena("Diagonal Blockage");
        using BenchmarkHarness harness = CreateHarness(context, mode, blockageLevel, playerCount);
        harness.Input.SetInput(harness.Simulation.Players[0].NetworkId, new PlayerInputState(-0.4f, false, false, false, false, null));
        harness.Input.SetInput(harness.Simulation.Players[1].NetworkId, new PlayerInputState(0.8f, false, false, false, false, null));
        int maxSegmentPenetrations = 0;
        harness.RunTicks(240, _ =>
        {
            Rope rope = harness.Simulation.Ropes[0];
            maxSegmentPenetrations = Math.Max(
                maxSegmentPenetrations,
                RopeCollisionValidator.CountSegmentPenetrations(rope, blockageLevel.Platforms));
        });

        result.Statistics.Set("segment_penetrations", maxSegmentPenetrations);
        result.Assertions.Add(maxSegmentPenetrations == 0
            ? BenchmarkAssertion.Pass("rope.diagonal_block", "Rope did not pass through matching platforms.", maxSegmentPenetrations)
            : BenchmarkAssertion.Fail("rope.diagonal_block", "Rope passed through matching platforms.", maxSegmentPenetrations));
        AddPhysics(result, harness, blockageLevel);
        return result;
    }

    private static RopeMechanicResult RunPullStability(
        BenchmarkContext context,
        RopeGameplayMode mode,
        Level level,
        int playerCount)
    {
        using BenchmarkHarness harness = CreateHarness(context, mode, level, playerCount);
        RopeMetrics metrics = new();
        float maxPathJump = 0f;
        float previousPath = harness.Simulation.Ropes[0].CurrentPathLength;
        harness.ApplyUniformInput(new PlayerInputState(0f, false, false, false, true, null));
        harness.RunTicks(180, _ =>
        {
            Rope rope = harness.Simulation.Ropes[0];
            metrics.Observe(rope);
            maxPathJump = MathF.Max(maxPathJump, MathF.Abs(rope.CurrentPathLength - previousPath));
            previousPath = rope.CurrentPathLength;
        });

        metrics.Finish();
        RopeMechanicResult result = CreateResult("pull_stability", "Pull Stability");
        WriteRopeMetrics(result, metrics);
        result.Statistics.Set("path_jump.max", maxPathJump);
        float pathJumpLimit = mode == RopeGameplayMode.ColoredPhysics ? 28f : 18f;
        result.Assertions.Add(maxPathJump <= pathJumpLimit
            ? BenchmarkAssertion.Pass("rope.pull_stable", "Pull rope movement stayed stable.", maxPathJump)
            : BenchmarkAssertion.Fail("rope.pull_stable", "Pull rope movement was erratic.", maxPathJump, 0f, pathJumpLimit));
        result.Assertions.Add(metrics.MaxOscillation <= 0.75f
            ? BenchmarkAssertion.Pass("rope.pull_tension", "Pull tension stayed smooth.", metrics.MaxOscillation)
            : BenchmarkAssertion.Fail("rope.pull_tension", "Pull tension oscillated too much.", metrics.MaxOscillation, 0f, 0.75f));
        AddPhysics(result, harness, level);
        return result;
    }

    private static RopeMechanicResult RunSegmentPenetrationGuard(
        BenchmarkContext context,
        RopeGameplayMode mode,
        Level level,
        int playerCount)
    {
        RopeMechanicResult result = CreateResult("penetration_guard", "Segment Penetration Guard");
        if (mode != RopeGameplayMode.ColoredPhysics)
        {
            result.Assertions.Add(BenchmarkAssertion.Pass("rope.penetration", "Skipped for regular rope."));
            return result;
        }

        using BenchmarkHarness harness = CreateHarness(context, mode, level, playerCount);
        SetOppositeHorizontal(harness, 1f);
        int maxPenetrations = 0;
        harness.RunTicks(300, _ =>
        {
            Rope rope = harness.Simulation.Ropes[0];
            maxPenetrations = Math.Max(
                maxPenetrations,
                RopeCollisionValidator.CountSegmentPenetrations(rope, level.Platforms));
        });

        result.Statistics.Set("segment_penetrations", maxPenetrations);
        result.Assertions.Add(maxPenetrations == 0
            ? BenchmarkAssertion.Pass("rope.penetration", "No rope segments penetrated matching platforms.", maxPenetrations)
            : BenchmarkAssertion.Fail("rope.penetration", "Rope segments penetrated matching platforms.", maxPenetrations));
        AddPhysics(result, harness, level);
        return result;
    }

    private static BenchmarkHarness CreateHarness(BenchmarkContext context, RopeGameplayMode mode, Level level, int playerCount)
    {
        BenchmarkHarness harness = context.CreateHarness(level, playerCount, mode, levelId: $"rope_{mode}");
        harness.RunTicks(30);
        ApplyPlayerColor(harness, GameColor.Red);
        return harness;
    }

    private static void ApplyPlayerColor(BenchmarkHarness harness, GameColor color)
    {
        harness.ApplyUniformInput(new PlayerInputState(0f, false, false, false, false, color));
        harness.RunTicks(2);
        harness.ApplyUniformInput(PlayerInputState.Empty);
    }

    private static void SetOppositeHorizontal(BenchmarkHarness harness, float direction)
    {
        harness.Input.SetInput(harness.Simulation.Players[0].NetworkId, new PlayerInputState(-direction, false, false, false, false, null));
        if (harness.Simulation.Players.Count > 1)
        {
            harness.Input.SetInput(harness.Simulation.Players[1].NetworkId, new PlayerInputState(direction, false, false, false, false, null));
        }
    }

    private static Dictionary<int, Vector2> CapturePositions(BenchmarkHarness harness)
    {
        Dictionary<int, Vector2> starts = new();
        foreach (Player player in harness.Simulation.Players)
        {
            starts[player.NetworkId] = player.Position;
        }

        return starts;
    }

    private static RopeMetrics SampleRope(BenchmarkHarness harness, int ticks)
    {
        RopeMetrics metrics = new();
        harness.RunTicks(ticks, _ => metrics.Observe(harness.Simulation.Ropes[0]));
        metrics.Finish();
        return metrics;
    }

    private static RopeMechanicResult CreateResult(string id, string name) =>
        new() { MechanicId = id, MechanicName = name };

    private static void WriteRopeMetrics(RopeMechanicResult result, RopeMetrics metrics)
    {
        result.Statistics.Set("tension.avg", metrics.AverageTension);
        result.Statistics.Set("tension.max", metrics.MaxTension);
        result.Statistics.Set("stretch.max", metrics.MaxStretch);
        result.Statistics.Set("compression.max", metrics.MaxCompression);
        result.Statistics.Set("pull_force.max", metrics.MaxPullForce);
        result.Statistics.Set("oscillation.max", metrics.MaxOscillation);
        result.Statistics.Set("slack.min", metrics.MinSlack);
    }

    private static void AddPhysics(RopeMechanicResult result, BenchmarkHarness harness, Level level)
    {
        result.Assertions.AddRange(BenchmarkPhysicsValidator.ValidateSimulation(harness.Simulation, level));
    }

    private sealed class RopeMetrics
    {
        public int SampleCount { get; private set; }
        public float AverageTension { get; private set; }
        public float MaxTension { get; private set; }
        public float MaxStretch { get; private set; }
        public float MaxCompression { get; private set; }
        public float MaxPullForce { get; private set; }
        public float MaxOscillation { get; private set; }
        public float MinSlack { get; private set; } = float.MaxValue;
        private float _previousTension;

        public void Observe(Rope rope)
        {
            SampleCount++;
            MaxTension = MathF.Max(MaxTension, rope.LastTension);
            AverageTension += rope.LastTension;
            float stretch = MathF.Max(0f, rope.CurrentPathLength - rope.TargetRestLength);
            float compression = MathF.Max(0f, rope.TargetRestLength - rope.CurrentPathLength);
            MaxStretch = MathF.Max(MaxStretch, stretch);
            MaxCompression = MathF.Max(MaxCompression, compression);
            MaxPullForce = MathF.Max(MaxPullForce, rope.LastEndpointForce);
            MinSlack = MathF.Min(MinSlack, rope.SlackAmount);
            if (SampleCount > 1)
            {
                MaxOscillation = MathF.Max(MaxOscillation, MathF.Abs(rope.LastTension - _previousTension));
            }

            _previousTension = rope.LastTension;
        }

        public void Finish()
        {
            if (SampleCount > 0)
            {
                AverageTension /= SampleCount;
            }
            else
            {
                AverageTension = 0f;
            }

            if (MinSlack == float.MaxValue)
            {
                MinSlack = 0f;
            }
        }
    }
}
