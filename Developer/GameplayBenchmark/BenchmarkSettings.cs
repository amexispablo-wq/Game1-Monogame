#nullable enable
using System;
using System.IO;
using System.Text.Json;

namespace ColorBlocks.Developer.GameplayBenchmark;

public sealed class BenchmarkSettings
{
    private static BenchmarkSettings? _active;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static BenchmarkSettings Active => _active ??= Load();

    public float MovementTolerance { get; set; } = 2.5f;
    public float ReplayPositionTolerance { get; set; } = 0.75f;
    public float ReplayRopeTolerance { get; set; } = 1.5f;
    public float GhostPositionTolerance { get; set; } = 1f;
    public float GhostVelocityTolerance { get; set; } = 3f;
    public double MaxSimulationMsPerTick { get; set; } = 4d;
    public double MaxFrameSpikeMs { get; set; } = 33d;
    public int MaxBenchmarkSeconds { get; set; } = 120;
    public int FuzzSimulationCount { get; set; } = 100;
    public int FuzzSeed { get; set; } = 0;
    public bool UseFixedFuzzSeed { get; set; }

    public static void Reload()
    {
        _active = Load();
    }

    public void Save()
    {
        string path = GetSettingsPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
        _active = this;
    }

    private static BenchmarkSettings Load()
    {
        string path = GetSettingsPath();
        if (!File.Exists(path))
        {
            return new BenchmarkSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<BenchmarkSettings>(File.ReadAllText(path)) ?? new BenchmarkSettings();
        }
        catch
        {
            return new BenchmarkSettings();
        }
    }

    private static string GetSettingsPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "Developer", "GameplayBenchmark", "settings.json");
    }
}
