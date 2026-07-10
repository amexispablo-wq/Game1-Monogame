#nullable enable
namespace ColorBlocks.Developer.GameplayBenchmark;

public sealed class BenchmarkAssertion
{
    public BenchmarkAssertion(string name, BenchmarkVerdict verdict, string message, float? measured = null, float? expected = null, float? tolerance = null)
    {
        Name = name;
        Verdict = verdict;
        Message = message;
        Measured = measured;
        Expected = expected;
        Tolerance = tolerance;
    }

    public string Name { get; }
    public BenchmarkVerdict Verdict { get; }
    public string Message { get; }
    public float? Measured { get; }
    public float? Expected { get; }
    public float? Tolerance { get; }

    public static BenchmarkAssertion Pass(string name, string message, float? measured = null)
    {
        return new BenchmarkAssertion(name, BenchmarkVerdict.Pass, message, measured);
    }

    public static BenchmarkAssertion Warn(string name, string message, float? measured = null, float? expected = null)
    {
        return new BenchmarkAssertion(name, BenchmarkVerdict.Warning, message, measured, expected);
    }

    public static BenchmarkAssertion Fail(string name, string message, float? measured = null, float? expected = null, float? tolerance = null)
    {
        return new BenchmarkAssertion(name, BenchmarkVerdict.Fail, message, measured, expected, tolerance);
    }
}
