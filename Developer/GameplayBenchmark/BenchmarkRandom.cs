#nullable enable
using System;

namespace ColorBlocks.Developer.GameplayBenchmark;

/// <summary>Deterministic RNG wrapper for fuzz and benchmark generation.</summary>
public sealed class BenchmarkRandom
{
    private readonly Random _random;

    public BenchmarkRandom(int seed)
    {
        Seed = seed;
        _random = new Random(seed);
    }

    public int Seed { get; }

    public int NextInt(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);

    public float NextFloat() => (float)_random.NextDouble();

    public float NextFloat(float minInclusive, float maxExclusive) => minInclusive + (NextFloat() * (maxExclusive - minInclusive));

    public bool NextBool(float trueChance = 0.5f) => NextFloat() < trueChance;

    public int NextInt(int maxExclusive) => _random.Next(maxExclusive);

    public T Pick<T>(T[] values) => Pick((ReadOnlySpan<T>)values);

    public T Pick<T>(ReadOnlySpan<T> values)
    {
        if (values.Length == 0)
        {
            throw new InvalidOperationException("Cannot pick from empty span.");
        }

        return values[NextInt(values.Length)];
    }
}
