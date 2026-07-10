#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ColorBlocks.Developer.GameplayBenchmark.Scenarios;

public sealed class PerformanceSimulationBenchmark : BenchmarkScenario
{
    public override string Id => "performance.simulation";
    public override string Name => "Performance Simulation";
    public override BenchmarkCategory Category => BenchmarkCategory.Performance;

    public override BenchmarkResult Run(BenchmarkContext context)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        using BenchmarkHarness harness = context.CreateHarness(
            BenchmarkLevelFactory.CreateRopeArena(),
            4,
            RopeGameplayMode.ColoredPhysics);

        harness.ApplyUniformInput(new PlayerInputState(1f, false, false, false, false, null));
        BenchmarkStatistics stats = new();
        List<BenchmarkAssertion> assertions = new();
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        double worstTickMs = 0d;
        double totalTickMs = 0d;
        int tickCount = 0;

        for (int i = 0; i < 300; i++)
        {
            Stopwatch tickWatch = Stopwatch.StartNew();
            harness.Simulation.Advance(harness.Simulation.TickRate.FixedDeltaSeconds, harness.Input);
            tickWatch.Stop();
            double tickMs = tickWatch.Elapsed.TotalMilliseconds;
            totalTickMs += tickMs;
            worstTickMs = Math.Max(worstTickMs, tickMs);
            tickCount++;
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        double avgTickMs = tickCount > 0 ? totalTickMs / tickCount : 0d;
        stats.Set("simulation.avg_ms", avgTickMs);
        stats.Set("simulation.worst_ms", worstTickMs);
        stats.Set("memory.allocated_bytes", allocated);
        stats.Set("fps.avg", avgTickMs > 0d ? 1000d / avgTickMs : 0d);
        stats.Set("fps.worst", worstTickMs > 0d ? 1000d / worstTickMs : 0d);

        assertions.Add(avgTickMs <= context.Settings.MaxSimulationMsPerTick
            ? BenchmarkAssertion.Pass("performance.simulation", "Average simulation tick within threshold.", (float)avgTickMs)
            : BenchmarkAssertion.Warn("performance.simulation", "Average simulation tick exceeded threshold.", (float)avgTickMs, (float)context.Settings.MaxSimulationMsPerTick));

        assertions.Add(worstTickMs <= context.Settings.MaxFrameSpikeMs
            ? BenchmarkAssertion.Pass("performance.spike", "Worst simulation tick within spike threshold.", (float)worstTickMs)
            : BenchmarkAssertion.Warn("performance.spike", "Simulation spike exceeded threshold.", (float)worstTickMs, (float)context.Settings.MaxFrameSpikeMs));

        return new BenchmarkResult(Id, Name, Category, BenchmarkVerdict.Pass, stopwatch.Elapsed, assertions, stats);
    }
}
