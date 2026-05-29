using System;

namespace ColorBlocks;

public readonly record struct SimulationTick(long Value) : IComparable<SimulationTick>
{
    public static SimulationTick Zero { get; } = new(0);

    public SimulationTick Next()
    {
        return new SimulationTick(Value + 1);
    }

    public int CompareTo(SimulationTick other)
    {
        return Value.CompareTo(other.Value);
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}
