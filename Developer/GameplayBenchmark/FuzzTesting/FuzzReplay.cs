#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ColorBlocks.Replay;

namespace ColorBlocks.Developer.GameplayBenchmark.FuzzTesting;

public static class FuzzReplay
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string SaveFailure(int seed, FuzzScenario scenario, FuzzResult result)
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "Developer", "FuzzFailures", $"Seed_{seed:D8}");
        Directory.CreateDirectory(directory);

        FuzzFailureReport report = new()
        {
            Seed = seed,
            FailureFrame = result.FailureFrame,
            FailureReason = result.FailureReason,
            PlayerCount = scenario.PlayerCount,
            RopeMode = scenario.RopeMode,
            LavaRiseEnabled = scenario.LavaRiseEnabled,
            Level = scenario.Level.ToData(),
            InputFrames = scenario.InputFrames,
            Assertions = result.Assertions,
            Statistics = result.Statistics.Values
        };

        File.WriteAllText(Path.Combine(directory, "Report.json"), JsonSerializer.Serialize(report, JsonOptions));

        if (result.Replay is not null)
        {
            ReplayFile file = ReplayFileSerializer.CreateFromSession(
                result.Replay,
                $"fuzz_{seed}",
                officialBestTime: 0f,
                playerCount: scenario.PlayerCount);
            ReplayFileSerializer.Save(Path.Combine(directory, "Replay.json"), file);
        }

        File.WriteAllText(
            Path.Combine(directory, "Inputs.json"),
            JsonSerializer.Serialize(scenario.InputFrames, JsonOptions));

        return directory;
    }

    private sealed class FuzzFailureReport
    {
        public int Seed { get; init; }
        public int FailureFrame { get; init; }
        public string FailureReason { get; init; } = string.Empty;
        public int PlayerCount { get; init; }
        public RopeGameplayMode RopeMode { get; init; }
        public bool LavaRiseEnabled { get; init; }
        public LevelData Level { get; init; } = new();
        public List<FuzzInputFrame> InputFrames { get; init; } = new();
        public List<BenchmarkAssertion> Assertions { get; init; } = new();
        public IReadOnlyDictionary<string, double> Statistics { get; init; } = new Dictionary<string, double>();
    }
}
