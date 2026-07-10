#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ColorBlocks.Developer.GameplayBenchmark;

public static class BenchmarkRegistry
{
    private static readonly List<BenchmarkScenario> Scenarios = new();
    private static bool _initialized;

    public static IReadOnlyList<BenchmarkScenario> All
    {
        get
        {
            EnsureInitialized();
            return Scenarios;
        }
    }

    public static IEnumerable<BenchmarkScenario> GetByCategory(BenchmarkCategory category)
    {
        return All.Where(scenario => scenario.Category == category);
    }

    public static BenchmarkScenario? FindById(string id)
    {
        return All.FirstOrDefault(scenario => string.Equals(scenario.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public static void Register(BenchmarkScenario scenario)
    {
        if (Scenarios.Any(existing => string.Equals(existing.Id, scenario.Id, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        Scenarios.Add(scenario);
    }

    private static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        RegisterFromAssembly(typeof(BenchmarkRegistry).Assembly);
    }

    private static void RegisterFromAssembly(Assembly assembly)
    {
        foreach (Type type in assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(BenchmarkScenario).IsAssignableFrom(type))
            {
                continue;
            }

            if (Activator.CreateInstance(type) is BenchmarkScenario scenario)
            {
                Register(scenario);
            }
        }
    }
}
