#nullable enable
using System.Collections.Generic;

namespace ColorBlocks.Developer.GameplayBenchmark.FuzzTesting;

public sealed class FuzzScenario
{
    public int Seed { get; init; }
    public Level Level { get; init; } = Level.CreateDefault();
    public int PlayerCount { get; init; } = 2;
    public RopeGameplayMode RopeMode { get; init; } = RopeGameplayMode.Neutral;
    public bool LavaRiseEnabled { get; init; }
    public bool ReplayEnabled { get; init; }
    public bool GhostEnabled { get; init; }
    public List<FuzzInputFrame> InputFrames { get; init; } = new();
    public int MaxTicks { get; init; } = 900;
}

public sealed class FuzzInputFrame
{
    public int Tick { get; init; }
    public float Horizontal { get; init; }
    public bool Jump { get; init; }
    public bool Pull { get; init; }
    public bool FastFall { get; init; }
    public bool Respawn { get; init; }
}
