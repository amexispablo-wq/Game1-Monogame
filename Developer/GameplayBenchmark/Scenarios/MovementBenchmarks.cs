#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Xna.Framework;

namespace ColorBlocks.Developer.GameplayBenchmark.Scenarios;

public sealed class MovementAccelerationBenchmark : BenchmarkScenario
{
    public override string Id => "movement.acceleration";
    public override string Name => "Movement Acceleration";
    public override BenchmarkCategory Category => BenchmarkCategory.Movement;

    public override BenchmarkResult Run(BenchmarkContext context)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        List<BenchmarkAssertion> assertions = new();
        BenchmarkStatistics stats = new();

        using BenchmarkHarness harness = context.CreateHarness(
            BenchmarkLevelFactory.CreateFlatMovementArena(),
            playerCount: 1,
            RopeGameplayMode.Neutral);

        Player player = harness.Simulation.Players[0];
        float startSpeed = MathF.Abs(player.Velocity.X);
        harness.ApplyUniformInput(new PlayerInputState(1f, false, false, false, false, null));
        harness.RunTicks(30, tick => context.SimulationFrame = tick, context.Settings.MaxBenchmarkSeconds);

        float endSpeed = MathF.Abs(player.Velocity.X);
        float acceleration = (endSpeed - startSpeed) / (30f / 60f);
        stats.Set("acceleration", acceleration);
        stats.Set("speed_end", endSpeed);

        assertions.Add(endSpeed > startSpeed
            ? BenchmarkAssertion.Pass("movement.acceleration", "Player accelerated with held input.", acceleration)
            : BenchmarkAssertion.Fail("movement.acceleration", "Player did not accelerate.", acceleration));

        assertions.AddRange(BenchmarkPhysicsValidator.ValidateSimulation(harness.Simulation, harness.Simulation.Level));
        return new BenchmarkResult(Id, Name, Category, BenchmarkVerdict.Pass, stopwatch.Elapsed, assertions, stats);
    }
}

public sealed class MovementMaxSpeedBenchmark : BenchmarkScenario
{
    public override string Id => "movement.max_speed";
    public override string Name => "Movement Maximum Speed";
    public override BenchmarkCategory Category => BenchmarkCategory.Movement;

    public override BenchmarkResult Run(BenchmarkContext context)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        List<BenchmarkAssertion> assertions = new();
        BenchmarkStatistics stats = new();

        using BenchmarkHarness harness = context.CreateHarness(
            BenchmarkLevelFactory.CreateFlatMovementArena(),
            playerCount: 1,
            RopeGameplayMode.Neutral);

        Player player = harness.Simulation.Players[0];
        harness.ApplyUniformInput(new PlayerInputState(1f, false, false, false, false, null));
        harness.RunTicks(240, tick => context.SimulationFrame = tick, context.Settings.MaxBenchmarkSeconds);

        float maxSpeed = MathF.Abs(player.Velocity.X);
        stats.Set("max_speed", maxSpeed);
        float tolerance = context.Settings.MovementTolerance;
        assertions.Add(MathF.Abs(maxSpeed - player.MaxMoveSpeed) <= tolerance
            ? BenchmarkAssertion.Pass("movement.max_speed", "Reached configured max move speed.", maxSpeed)
            : BenchmarkAssertion.Fail("movement.max_speed", "Max speed mismatch.", maxSpeed, player.MaxMoveSpeed, tolerance));

        return new BenchmarkResult(Id, Name, Category, BenchmarkVerdict.Pass, stopwatch.Elapsed, assertions, stats);
    }
}

public sealed class MovementStoppingDistanceBenchmark : BenchmarkScenario
{
    public override string Id => "movement.stopping";
    public override string Name => "Movement Stopping Distance";
    public override BenchmarkCategory Category => BenchmarkCategory.Movement;

    public override BenchmarkResult Run(BenchmarkContext context)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        List<BenchmarkAssertion> assertions = new();
        BenchmarkStatistics stats = new();

        using BenchmarkHarness harness = context.CreateHarness(
            BenchmarkLevelFactory.CreateFlatMovementArena(),
            playerCount: 1,
            RopeGameplayMode.Neutral);

        Player player = harness.Simulation.Players[0];
        harness.ApplyUniformInput(new PlayerInputState(1f, false, false, false, false, null));
        harness.RunTicks(240);
        float peakX = player.Position.X;
        harness.ApplyUniformInput(PlayerInputState.Empty);
        harness.RunTicks(120);
        float stopX = player.Position.X;
        float coast = stopX - peakX;
        stats.Set("stopping_distance", coast);

        assertions.Add(coast > 0f
            ? BenchmarkAssertion.Pass("movement.stopping", "Player coasted after input release.", coast)
            : BenchmarkAssertion.Warn("movement.stopping", "Stopping distance was unexpectedly small.", coast));

        return new BenchmarkResult(Id, Name, Category, BenchmarkVerdict.Pass, stopwatch.Elapsed, assertions, stats);
    }
}

public sealed class MovementJumpHeightBenchmark : BenchmarkScenario
{
    public override string Id => "movement.jump";
    public override string Name => "Movement Jump Height";
    public override BenchmarkCategory Category => BenchmarkCategory.Movement;

    public override BenchmarkResult Run(BenchmarkContext context)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        List<BenchmarkAssertion> assertions = new();
        BenchmarkStatistics stats = new();

        using BenchmarkHarness harness = context.CreateHarness(
            BenchmarkLevelFactory.CreateFlatMovementArena(),
            playerCount: 1,
            RopeGameplayMode.Neutral);

        Player player = harness.Simulation.Players[0];
        harness.ApplyUniformInput(PlayerInputState.Empty);
        harness.RunTicks(45, _ => context.SimulationFrame = _);

        float groundY = player.Position.Y;
        float minY = groundY;
        harness.ApplyUniformInput(new PlayerInputState(0f, true, false, false, false, null));
        harness.RunTicks(1);
        harness.ApplyUniformInput(PlayerInputState.Empty);
        harness.RunTicks(120, _ =>
        {
            minY = MathF.Min(minY, player.Position.Y);
        });

        float jumpHeight = groundY - minY;
        stats.Set("jump_height", jumpHeight);
        assertions.Add(jumpHeight > 24f
            ? BenchmarkAssertion.Pass("movement.jump", "Jump produced vertical displacement.", jumpHeight)
            : BenchmarkAssertion.Fail("movement.jump", "Jump height too small.", jumpHeight));

        return new BenchmarkResult(Id, Name, Category, BenchmarkVerdict.Pass, stopwatch.Elapsed, assertions, stats);
    }
}

public sealed class MovementParityBenchmark : BenchmarkScenario
{
    public override string Id => "movement.parity";
    public override string Name => "Movement Player Parity";
    public override BenchmarkCategory Category => BenchmarkCategory.Movement;

    public override BenchmarkResult Run(BenchmarkContext context)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        List<BenchmarkAssertion> assertions = new();
        BenchmarkStatistics stats = new();
        float tolerance = context.Settings.MovementTolerance;

        for (int playerCount = 1; playerCount <= PartyManager.MaxMembers; playerCount++)
        {
            context.CurrentAssertion = $"parity_{playerCount}p";
            using BenchmarkHarness harness = context.CreateHarness(
                BenchmarkLevelFactory.CreateFlatMovementArena($"Parity {playerCount}P"),
                playerCount,
                RopeGameplayMode.Neutral);

            harness.ApplyUniformInput(new PlayerInputState(1f, false, false, false, false, null));
            Dictionary<int, Vector2> startPositions = harness.Simulation.Players.ToDictionary(
                static player => player.NetworkId,
                static player => player.Position);
            harness.RunTicks(180, tick => context.SimulationFrame = tick, context.Settings.MaxBenchmarkSeconds);

            Player reference = harness.Simulation.Players[0];
            Vector2 referenceDisplacement = reference.Position - startPositions[reference.NetworkId];
            float maxDelta = 0f;
            foreach (Player player in harness.Simulation.Players.Skip(1))
            {
                Vector2 displacement = player.Position - startPositions[player.NetworkId];
                float displacementDelta = Vector2.Distance(referenceDisplacement, displacement);
                float velDelta = MathF.Abs(reference.Velocity.X - player.Velocity.X);
                maxDelta = MathF.Max(maxDelta, MathF.Max(displacementDelta, velDelta));
            }

            stats.Set($"parity_{playerCount}p.max_delta", maxDelta);
            assertions.Add(maxDelta <= tolerance
                ? BenchmarkAssertion.Pass($"movement.parity.{playerCount}", $"{playerCount} players stayed aligned.", maxDelta)
                : BenchmarkAssertion.Fail($"movement.parity.{playerCount}", $"{playerCount} players diverged.", maxDelta, 0f, tolerance));
        }

        return new BenchmarkResult(Id, Name, Category, BenchmarkVerdict.Pass, stopwatch.Elapsed, assertions, stats);
    }
}

public sealed class MovementAirControlBenchmark : BenchmarkScenario
{
    public override string Id => "movement.air_control";
    public override string Name => "Movement Air Control";
    public override BenchmarkCategory Category => BenchmarkCategory.Movement;

    public override BenchmarkResult Run(BenchmarkContext context)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        using BenchmarkHarness harness = context.CreateHarness(
            BenchmarkLevelFactory.CreateFlatMovementArena(),
            1,
            RopeGameplayMode.Neutral);

        Player player = harness.Simulation.Players[0];
        harness.ApplyUniformInput(new PlayerInputState(0f, true, false, false, false, null));
        harness.RunTicks(12);
        harness.ApplyUniformInput(new PlayerInputState(1f, false, false, false, false, null));
        float vxBefore = player.Velocity.X;
        harness.RunTicks(20);
        float vxAfter = player.Velocity.X;

        List<BenchmarkAssertion> assertions = new()
        {
            vxAfter > vxBefore
                ? BenchmarkAssertion.Pass("movement.air_control", "Air control changed horizontal velocity.", vxAfter - vxBefore)
                : BenchmarkAssertion.Warn("movement.air_control", "Air control effect was weak.", vxAfter - vxBefore)
        };

        return new BenchmarkResult(Id, Name, Category, BenchmarkVerdict.Pass, stopwatch.Elapsed, assertions);
    }
}

public sealed class MovementFastFallBenchmark : BenchmarkScenario
{
    public override string Id => "movement.fast_fall";
    public override string Name => "Movement Fast Fall";
    public override BenchmarkCategory Category => BenchmarkCategory.Movement;

    public override BenchmarkResult Run(BenchmarkContext context)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        using BenchmarkHarness harness = context.CreateHarness(
            BenchmarkLevelFactory.CreateFlatMovementArena(),
            1,
            RopeGameplayMode.Neutral);

        Player player = harness.Simulation.Players[0];
        harness.ApplyUniformInput(new PlayerInputState(0f, true, false, false, false, null));
        harness.RunTicks(20);
        float normalVy = player.Velocity.Y;
        harness.ApplyUniformInput(new PlayerInputState(0f, false, false, true, false, null));
        harness.RunTicks(20);
        float fastVy = player.Velocity.Y;

        List<BenchmarkAssertion> assertions = new()
        {
            fastVy > normalVy
                ? BenchmarkAssertion.Pass("movement.fast_fall", "Fast fall increased downward velocity.", fastVy)
                : BenchmarkAssertion.Fail("movement.fast_fall", "Fast fall did not increase downward velocity.", fastVy, normalVy)
        };

        return new BenchmarkResult(Id, Name, Category, BenchmarkVerdict.Pass, stopwatch.Elapsed, assertions);
    }
}
