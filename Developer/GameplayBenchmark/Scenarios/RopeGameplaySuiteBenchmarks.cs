#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ColorBlocks.Developer.GameplayBenchmark.Scenarios;

public sealed class RegularRopeGameplaySuiteBenchmark : BenchmarkScenario
{
    public override string Id => "rope.regular.suite";
    public override string Name => "Regular Rope Gameplay Suite";
    public override BenchmarkCategory Category => BenchmarkCategory.Rope;

    public override BenchmarkResult Run(BenchmarkContext context) =>
        RopeGameplaySuiteBenchmarkHelper.RunSuite(context, RopeGameplayMode.Neutral, BenchmarkLevelFactory.CreateRopeArena("Regular Rope Suite"));
}

public sealed class ColoredRopeGameplaySuiteBenchmark : BenchmarkScenario
{
    public override string Id => "rope.colored.suite";
    public override string Name => "Colored Rope Gameplay Suite";
    public override BenchmarkCategory Category => BenchmarkCategory.Rope;

    public override BenchmarkResult Run(BenchmarkContext context) =>
        RopeGameplaySuiteBenchmarkHelper.RunSuite(context, RopeGameplayMode.ColoredPhysics, BenchmarkLevelFactory.CreateRopeArena("Colored Rope Suite"));
}

internal static class RopeGameplaySuiteBenchmarkHelper
{
    public static BenchmarkResult RunSuite(BenchmarkContext context, RopeGameplayMode mode, Level level)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        List<BenchmarkAssertion> assertions = new();
        BenchmarkStatistics stats = new();
        string modeLabel = mode == RopeGameplayMode.Neutral ? "regular" : "colored";

        foreach (RopeMechanicResult mechanic in RopeMechanicsSimulation.RunAll(context, mode, level))
        {
            context.CurrentAssertion = $"{modeLabel}.{mechanic.MechanicId}";
            foreach (KeyValuePair<string, double> entry in mechanic.Statistics.Values)
            {
                stats.Set($"{mechanic.MechanicId}.{entry.Key}", entry.Value);
            }

            foreach (BenchmarkAssertion assertion in mechanic.Assertions)
            {
                assertions.Add(new BenchmarkAssertion(
                    $"{modeLabel}.{mechanic.MechanicId}.{assertion.Name}",
                    assertion.Verdict,
                    $"[{mechanic.MechanicName}] {assertion.Message}",
                    assertion.Measured,
                    assertion.Expected,
                    assertion.Tolerance));
            }
        }

        BenchmarkVerdict verdict = BenchmarkResult.ResolveVerdict(BenchmarkVerdict.Pass, assertions);
        return new BenchmarkResult(
            mode == RopeGameplayMode.Neutral ? "rope.regular.suite" : "rope.colored.suite",
            mode == RopeGameplayMode.Neutral ? "Regular Rope Gameplay Suite" : "Colored Rope Gameplay Suite",
            BenchmarkCategory.Rope,
            verdict,
            stopwatch.Elapsed,
            assertions,
            stats);
    }
}
