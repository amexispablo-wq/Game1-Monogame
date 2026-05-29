using System.Collections.Generic;

namespace ColorBlocks;

public sealed class GameSnapshot
{
    public long Tick { get; init; }
    public int Sequence { get; init; }
    public LevelSnapshot Level { get; init; }
    public TimerSnapshot Timer { get; init; }
    public RopeGameplayMode RopeMode { get; init; }
    public List<PlayerSnapshot> Players { get; init; } = new();
    public List<RopeSnapshot> Ropes { get; init; } = new();
}
