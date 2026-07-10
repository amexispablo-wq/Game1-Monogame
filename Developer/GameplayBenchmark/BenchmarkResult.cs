#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorBlocks.Developer.GameplayBenchmark;

public sealed class BenchmarkResult
{
    public BenchmarkResult(
        string scenarioId,
        string scenarioName,
        BenchmarkCategory category,
        BenchmarkVerdict verdict,
        TimeSpan duration,
        IEnumerable<BenchmarkAssertion> assertions,
        BenchmarkStatistics? statistics = null,
        string? notes = null)
    {
        ScenarioId = scenarioId;
        ScenarioName = scenarioName;
        Category = category;
        Duration = duration;
        Assertions = assertions.ToList();
        Statistics = statistics ?? new BenchmarkStatistics();
        Notes = notes ?? string.Empty;
        Verdict = ResolveVerdict(verdict, Assertions);
    }

    public string ScenarioId { get; }
    public string ScenarioName { get; }
    public BenchmarkCategory Category { get; }
    public BenchmarkVerdict Verdict { get; }
    public TimeSpan Duration { get; }
    public List<BenchmarkAssertion> Assertions { get; }
    public BenchmarkStatistics Statistics { get; }
    public string Notes { get; }

    public static BenchmarkVerdict ResolveVerdict(BenchmarkVerdict baseVerdict, IReadOnlyList<BenchmarkAssertion> assertions)
    {
        BenchmarkVerdict verdict = baseVerdict;
        foreach (BenchmarkAssertion assertion in assertions)
        {
            if (assertion.Verdict == BenchmarkVerdict.Fail)
            {
                return BenchmarkVerdict.Fail;
            }

            if (assertion.Verdict == BenchmarkVerdict.Warning && verdict == BenchmarkVerdict.Pass)
            {
                verdict = BenchmarkVerdict.Warning;
            }
        }

        return verdict;
    }
}
