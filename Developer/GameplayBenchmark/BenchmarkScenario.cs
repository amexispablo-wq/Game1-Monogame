#nullable enable
namespace ColorBlocks.Developer.GameplayBenchmark;

public abstract class BenchmarkScenario
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract BenchmarkCategory Category { get; }

    public abstract BenchmarkResult Run(BenchmarkContext context);
}
