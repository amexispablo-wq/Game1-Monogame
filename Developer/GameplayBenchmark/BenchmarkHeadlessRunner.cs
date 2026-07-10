#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using ColorBlocks.Developer.GameplayBenchmark.FuzzTesting;

namespace ColorBlocks.Developer.GameplayBenchmark;

/// <summary>Runs benchmarks without launching the game window.</summary>
public static class BenchmarkHeadlessRunner
{
    public static int Execute(string[] args)
    {
        if (!DeveloperSettings.DeveloperMode)
        {
            Console.Error.WriteLine("Benchmark CLI requires developerMode=true in developer_settings.json");
            return 2;
        }

        BenchmarkSettings.Reload();
        BenchmarkRunner runner = new();
        string mode = args.Length > 1 ? args[1].ToLowerInvariant() : "all";

        switch (mode)
        {
            case "all":
                runner.StartAll();
                break;
            case "movement":
                runner.StartCategory(BenchmarkCategory.Movement);
                break;
            case "rope":
                runner.StartCategory(BenchmarkCategory.Rope);
                break;
            case "replay":
                runner.StartCategory(BenchmarkCategory.Replay);
                break;
            case "ghost":
                runner.StartCategory(BenchmarkCategory.Ghost);
                break;
            case "performance":
                runner.StartCategory(BenchmarkCategory.Performance);
                break;
            default:
                if (mode.StartsWith("fuzz:", StringComparison.Ordinal))
                {
                    int count = int.TryParse(mode.AsSpan(5), out int parsed) ? parsed : BenchmarkSettings.Active.FuzzSimulationCount;
                    runner.StartFuzz(count);
                }
                else if (mode.StartsWith("seed:", StringComparison.Ordinal))
                {
                    if (!int.TryParse(mode.AsSpan(5), out int seed))
                    {
                        Console.Error.WriteLine($"Invalid seed: {mode}");
                        return 2;
                    }

                    runner.StartReproduceSeed(seed);
                }
                else
                {
                    PrintUsage();
                    return 2;
                }

                break;
        }

        Stopwatch total = Stopwatch.StartNew();
        while (runner.Mode is BenchmarkRunMode.RunningAll
            or BenchmarkRunMode.RunningCategory
            or BenchmarkRunMode.RunningFuzz
            or BenchmarkRunMode.RunningReproduce)
        {
            runner.Update(double.MaxValue);
        }

        total.Stop();
        PrintReport(runner, total.Elapsed);
        return runner.LastReport?.FailCount > 0 ? 1 : 0;
    }

    private static void PrintReport(BenchmarkRunner runner, TimeSpan elapsed)
    {
        BenchmarkReport? report = runner.LastReport;
        if (report is null)
        {
            Console.WriteLine("No benchmark report generated.");
            return;
        }

        Console.WriteLine($"=== Gameplay Benchmark ===");
        Console.WriteLine($"Duration: {elapsed.TotalSeconds:0.###}s");
        Console.WriteLine($"PASS {report.PassCount}  WARN {report.WarningCount}  FAIL {report.FailCount}");
        Console.WriteLine();

        foreach (BenchmarkResult result in report.Results)
        {
            Console.WriteLine($"[{result.Verdict}] {result.ScenarioName} ({result.Duration.TotalMilliseconds:0}ms)");
            foreach (BenchmarkAssertion assertion in result.Assertions)
            {
                string measured = assertion.Measured.HasValue ? $" measured={assertion.Measured:0.###}" : string.Empty;
                string expected = assertion.Expected.HasValue ? $" expected={assertion.Expected:0.###}" : string.Empty;
                Console.WriteLine($"  - {assertion.Verdict} {assertion.Name}: {assertion.Message}{measured}{expected}");
            }

            foreach (KeyValuePair<string, double> stat in result.Statistics.Values.OrderBy(pair => pair.Key))
            {
                Console.WriteLine($"  * {stat.Key} = {stat.Value:0.###}");
            }

            if (!string.IsNullOrWhiteSpace(result.Notes))
            {
                Console.WriteLine($"  ! {result.Notes}");
            }

            Console.WriteLine();
        }

        string exportPath = BenchmarkExporter.Export(report);
        Console.WriteLine($"Report saved: {exportPath}");
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  Color Blocks.exe --benchmark all");
        Console.WriteLine("  Color Blocks.exe --benchmark movement|rope|replay|ghost|performance");
        Console.WriteLine("  Color Blocks.exe --benchmark fuzz:100");
        Console.WriteLine("  Color Blocks.exe --benchmark seed:48273195");
    }
}
