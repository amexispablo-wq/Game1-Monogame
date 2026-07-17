#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColorBlocks.Developer.GameplayBenchmark;

public static class BenchmarkExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Export(BenchmarkReport report)
    {
        Directory.CreateDirectory(UserDataPaths.Benchmarks);
        string path = UserDataPaths.BenchmarkReportFile;
        File.WriteAllText(path, JsonSerializer.Serialize(report, JsonOptions));
        return path;
    }

    public static BenchmarkReport CreateReport(IEnumerable<BenchmarkResult> results, TimeSpan totalDuration, IEnumerable<int>? fuzzSeeds = null)
    {
        List<BenchmarkResult> list = results.ToList();
        BenchmarkReport report = new()
        {
            GameVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0",
            DateUtc = DateTime.UtcNow,
            DeveloperMode = DeveloperSettings.DeveloperMode,
            TotalDuration = totalDuration,
            Results = list,
            FuzzSeeds = fuzzSeeds?.ToList() ?? new List<int>()
        };

        foreach (BenchmarkResult result in list)
        {
            switch (result.Verdict)
            {
                case BenchmarkVerdict.Pass:
                    report.PassCount++;
                    break;
                case BenchmarkVerdict.Warning:
                    report.WarningCount++;
                    break;
                case BenchmarkVerdict.Fail:
                    report.FailCount++;
                    break;
            }
        }

        return report;
    }
}
