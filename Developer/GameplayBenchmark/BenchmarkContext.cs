#nullable enable
using System;
using System.Diagnostics;

namespace ColorBlocks.Developer.GameplayBenchmark;

public sealed class BenchmarkContext
{
    public BenchmarkContext(BenchmarkSettings settings, int? seed = null)
    {
        Settings = settings;
        int resolvedSeed = seed ?? (settings.UseFixedFuzzSeed ? settings.FuzzSeed : Environment.TickCount);
        Random = new BenchmarkRandom(resolvedSeed);
        Stopwatch = Stopwatch.StartNew();
    }

    public BenchmarkSettings Settings { get; }
    public BenchmarkRandom Random { get; }
    public Stopwatch Stopwatch { get; }
    public string? CurrentAssertion { get; set; }
    public long SimulationFrame { get; set; }
    public int? CurrentSeed { get; set; }

    public BenchmarkHarness CreateHarness(
        Level level,
        int playerCount,
        RopeGameplayMode ropeMode,
        bool lavaRiseEnabled = false,
        string levelId = "benchmark")
    {
        return BenchmarkHarness.Create(level, playerCount, ropeMode, lavaRiseEnabled, levelId);
    }
}
