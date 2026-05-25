using System;

namespace Game1_Monogame;

public readonly record struct TickRate(int TicksPerSecond)
{
    public static TickRate Default { get; } = new(60);

    public float FixedDeltaSeconds => 1f / Math.Max(1, TicksPerSecond);
    public TimeSpan FixedDeltaTime => TimeSpan.FromSeconds(FixedDeltaSeconds);
}
