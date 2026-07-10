#nullable enable
using System;
using System.Collections.Generic;

namespace ColorBlocks.Developer.GameplayBenchmark;

public sealed class BenchmarkReport
{
    public string GameVersion { get; init; } = "0.0.0";
    public DateTime DateUtc { get; init; } = DateTime.UtcNow;
    public bool DeveloperMode { get; init; }
    public TimeSpan TotalDuration { get; init; }
    public List<BenchmarkResult> Results { get; init; } = new();
    public List<int> FuzzSeeds { get; init; } = new();
    public BenchmarkStatistics Performance { get; init; } = new();

    public int PassCount { get; set; }
    public int WarningCount { get; set; }
    public int FailCount { get; set; }
}
