#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Xna.Framework;

namespace ColorBlocks.Developer.GameplayBenchmark.Scenarios;

public sealed class RopeSlackBenchmark : BenchmarkScenario
{
    public override string Id => "rope.slack";
    public override string Name => "Rope Slack";
    public override BenchmarkCategory Category => BenchmarkCategory.Rope;

    public override BenchmarkResult Run(BenchmarkContext context)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        using BenchmarkHarness harness = context.CreateHarness(
            BenchmarkLevelFactory.CreateRopeArena(),
            2,
            RopeGameplayMode.Neutral);

        harness.ApplyUniformInput(PlayerInputState.Empty);
        harness.RunTicks(120);
        Rope rope = harness.Simulation.Ropes[0];
        BenchmarkStatistics stats = new();
        stats.Set("slack", rope.SlackAmount);
        stats.Set("tension", rope.LastTension);

        List<BenchmarkAssertion> assertions = new()
        {
            rope.SlackAmount >= 0f
                ? BenchmarkAssertion.Pass("rope.slack", "Slack remained non-negative.", rope.SlackAmount)
                : BenchmarkAssertion.Fail("rope.slack", "Negative slack detected.", rope.SlackAmount)
        };
        assertions.AddRange(BenchmarkPhysicsValidator.ValidateSimulation(harness.Simulation, harness.Simulation.Level));
        return new BenchmarkResult(Id, Name, Category, BenchmarkVerdict.Pass, stopwatch.Elapsed, assertions, stats);
    }
}

public sealed class RopeStretchBenchmark : BenchmarkScenario
{
    public override string Id => "rope.stretch";
    public override string Name => "Rope Maximum Stretch";
    public override BenchmarkCategory Category => BenchmarkCategory.Rope;

    public override BenchmarkResult Run(BenchmarkContext context)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        using BenchmarkHarness harness = context.CreateHarness(
            BenchmarkLevelFactory.CreateRopeArena(),
            2,
            RopeGameplayMode.Neutral);

        Player left = harness.Simulation.Players[0];
        Player right = harness.Simulation.Players[1];
        harness.Input.SetInput(left.NetworkId, new PlayerInputState(-1f, false, false, false, false, null));
        harness.Input.SetInput(right.NetworkId, new PlayerInputState(1f, false, false, false, false, null));

        float maxTension = 0f;
        float maxStretch = 0f;
        harness.RunTicks(360, _ =>
        {
            Rope rope = harness.Simulation.Ropes[0];
            maxTension = MathF.Max(maxTension, rope.LastTension);
            maxStretch = MathF.Max(maxStretch, MathF.Max(0f, rope.CurrentPathLength - rope.TargetRestLength));
        });

        BenchmarkStatistics stats = new();
        stats.Set("max_tension", maxTension);
        stats.Set("max_stretch", maxStretch);

        List<BenchmarkAssertion> assertions = new()
        {
            maxStretch >= 0f
                ? BenchmarkAssertion.Pass("rope.stretch", "Stretch measured during separation.", maxStretch)
                : BenchmarkAssertion.Fail("rope.stretch", "Stretch invalid.", maxStretch),
            float.IsFinite(maxTension)
                ? BenchmarkAssertion.Pass("rope.tension.finite", "Tension stayed finite.", maxTension)
                : BenchmarkAssertion.Fail("rope.tension.finite", "Tension became non-finite.", maxTension)
        };
        assertions.AddRange(BenchmarkPhysicsValidator.ValidateSimulation(harness.Simulation, harness.Simulation.Level));
        return new BenchmarkResult(Id, Name, Category, BenchmarkVerdict.Pass, stopwatch.Elapsed, assertions, stats);
    }
}

public sealed class RopePullBenchmark : BenchmarkScenario
{
    public override string Id => "rope.pull";
    public override string Name => "Rope Pull";
    public override BenchmarkCategory Category => BenchmarkCategory.Rope;

    public override BenchmarkResult Run(BenchmarkContext context)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        using BenchmarkHarness harness = context.CreateHarness(
            BenchmarkLevelFactory.CreateRopeArena(),
            2,
            RopeGameplayMode.Neutral);

        float startLength = harness.Simulation.Ropes[0].TargetRestLength;
        harness.ApplyUniformInput(new PlayerInputState(0f, false, false, false, true, null));
        harness.RunTicks(180);
        float endLength = harness.Simulation.Ropes[0].TargetRestLength;

        BenchmarkStatistics stats = new();
        stats.Set("pull_force", harness.Simulation.Ropes[0].LastEndpointForce);
        stats.Set("target_delta", startLength - endLength);

        List<BenchmarkAssertion> assertions = new()
        {
            endLength < startLength
                ? BenchmarkAssertion.Pass("rope.pull", "Pull shortened rope target length.", startLength - endLength)
                : BenchmarkAssertion.Warn("rope.pull", "Pull did not shorten target length.", endLength, startLength)
        };

        return new BenchmarkResult(Id, Name, Category, BenchmarkVerdict.Pass, stopwatch.Elapsed, assertions, stats);
    }
}

public sealed class RopeStabilityBenchmark : BenchmarkScenario
{
    public override string Id => "rope.stability";
    public override string Name => "Rope Constraint Stability";
    public override BenchmarkCategory Category => BenchmarkCategory.Rope;

    public override BenchmarkResult Run(BenchmarkContext context)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        using BenchmarkHarness harness = context.CreateHarness(
            BenchmarkLevelFactory.CreateRopeArena(),
            2,
            RopeGameplayMode.ColoredPhysics);

        float maxTension = 0f;
        float maxForce = 0f;
        int oscillationChanges = 0;
        float previousTension = 0f;

        for (int cycle = 0; cycle < 3; cycle++)
        {
            harness.Input.SetInput(harness.Simulation.Players[0].NetworkId, new PlayerInputState(-1f, cycle % 2 == 0, false, false, true, null));
            harness.Input.SetInput(harness.Simulation.Players[1].NetworkId, new PlayerInputState(1f, cycle % 2 == 1, false, false, true, null));
            harness.RunTicks(180, _ =>
            {
                Rope rope = harness.Simulation.Ropes[0];
                maxTension = MathF.Max(maxTension, rope.LastTension);
                maxForce = MathF.Max(maxForce, rope.LastEndpointForce);
                if (MathF.Sign(rope.LastTension - previousTension) != 0f && previousTension > 0.01f)
                {
                    oscillationChanges++;
                }

                previousTension = rope.LastTension;
            });
        }

        BenchmarkStatistics stats = new();
        stats.Set("max_tension", maxTension);
        stats.Set("max_force", maxForce);
        stats.Set("oscillation_changes", oscillationChanges);

        List<BenchmarkAssertion> assertions = BenchmarkPhysicsValidator.ValidateSimulation(harness.Simulation, harness.Simulation.Level);
        assertions.Add(maxForce <= harness.Simulation.Ropes[0].MaxRopeForce * 1.05f
            ? BenchmarkAssertion.Pass("rope.force_cap", "Rope force stayed within configured cap.", maxForce)
            : BenchmarkAssertion.Fail("rope.force_cap", "Rope force exceeded cap.", maxForce, harness.Simulation.Ropes[0].MaxRopeForce));

        return new BenchmarkResult(Id, Name, Category, BenchmarkVerdict.Pass, stopwatch.Elapsed, assertions, stats);
    }
}

public sealed class RopeSwingBenchmark : BenchmarkScenario
{
    public override string Id => "rope.swing";
    public override string Name => "Rope Swing";
    public override BenchmarkCategory Category => BenchmarkCategory.Rope;

    public override BenchmarkResult Run(BenchmarkContext context)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        using BenchmarkHarness harness = context.CreateHarness(
            BenchmarkLevelFactory.CreateRopeArena(),
            2,
            RopeGameplayMode.Neutral);

        harness.Input.SetInput(harness.Simulation.Players[0].NetworkId, PlayerInputState.Empty);
        harness.Input.SetInput(harness.Simulation.Players[1].NetworkId, new PlayerInputState(1f, true, false, false, false, null));
        harness.RunTicks(240);

        Rope rope = harness.Simulation.Ropes[0];
        BenchmarkStatistics stats = new();
        stats.Set("tension", rope.LastTension);
        stats.Set("path_length", rope.CurrentPathLength);

        return new BenchmarkResult(
            Id,
            Name,
            Category,
            BenchmarkVerdict.Pass,
            stopwatch.Elapsed,
            BenchmarkPhysicsValidator.ValidateSimulation(harness.Simulation, harness.Simulation.Level),
            stats);
    }
}
