#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ColorBlocks.Replay;

namespace ColorBlocks.Developer.GameplayBenchmark.FuzzTesting;

public sealed class FuzzResult
{
    public int Seed { get; init; }
    public bool Passed { get; init; }
    public int FailureFrame { get; init; }
    public string FailureReason { get; init; } = string.Empty;
    public BenchmarkStatistics Statistics { get; init; } = new();
    public List<BenchmarkAssertion> Assertions { get; init; } = new();
    public ReplayData? Replay { get; init; }

    public BenchmarkResult ToBenchmarkResult()
    {
        return new BenchmarkResult(
            $"fuzz.{Seed}",
            $"Fuzz {Seed}",
            BenchmarkCategory.Fuzz,
            Passed ? BenchmarkVerdict.Pass : BenchmarkVerdict.Fail,
            TimeSpan.Zero,
            Assertions,
            Statistics,
            FailureReason);
    }
}

public static class FuzzScenarioRunner
{
    public static FuzzResult Run(BenchmarkContext context, FuzzScenario scenario)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        List<BenchmarkAssertion> assertions = new();
        BenchmarkStatistics stats = new();
        ReplayRecorder? recorder = scenario.ReplayEnabled ? new ReplayRecorder() : null;

        using BenchmarkHarness harness = BenchmarkHarness.Create(
            scenario.Level,
            scenario.PlayerCount,
            scenario.RopeMode,
            scenario.LavaRiseEnabled,
            $"fuzz_{scenario.Seed}");

        if (recorder is not null)
        {
            recorder.StartRecording(
                $"fuzz_{scenario.Seed}",
                scenario.RopeMode,
                scenario.LavaRiseEnabled,
                harness.Simulation.TickRate.TicksPerSecond,
                harness.Simulation.LavaRiseSpeed,
                harness.Simulation.LavaSurfaceY,
                scenario.Level.ToData(),
                ReplayRecordingMode.FullSession);
        }

        int frame = 0;
        int failureFrame = -1;
        string failureReason = string.Empty;
        bool passed = true;
        int inputIndex = 0;
        float dt = harness.Simulation.TickRate.FixedDeltaSeconds;
        int maxTicks = Math.Min(scenario.MaxTicks, context.Settings.MaxBenchmarkSeconds * harness.Simulation.TickRate.TicksPerSecond);

        while (frame < maxTicks)
        {
            context.SimulationFrame = frame;
            ApplyFuzzInput(harness, scenario, frame, ref inputIndex);

            int steps = harness.Simulation.Advance(dt, harness.Input);
            if (steps <= 0)
            {
                break;
            }

            for (int i = 0; i < steps; i++)
            {
                frame++;
                recorder?.RecordFrame(harness.Simulation, harness.Camera);
                List<BenchmarkAssertion> physics = BenchmarkPhysicsValidator.ValidateSimulation(harness.Simulation, scenario.Level);
                BenchmarkAssertion? failure = physics.FirstOrDefault(assertion => assertion.Verdict == BenchmarkVerdict.Fail);
                if (failure is not null)
                {
                    passed = false;
                    failureFrame = frame;
                    failureReason = failure.Message;
                    assertions.Add(failure);
                    break;
                }

                if (stopwatch.Elapsed.TotalSeconds > context.Settings.MaxBenchmarkSeconds)
                {
                    passed = false;
                    failureFrame = frame;
                    failureReason = "Exceeded max benchmark execution time.";
                    assertions.Add(BenchmarkAssertion.Fail("fuzz.timeout", failureReason));
                    break;
                }
            }

            if (!passed)
            {
                break;
            }

            if (harness.Simulation.IsLevelComplete || harness.Simulation.IsPlayerDead)
            {
                break;
            }
        }

        assertions.AddRange(BenchmarkPhysicsValidator.ValidateSimulation(harness.Simulation, scenario.Level));
        stats.Set("ticks", frame);
        stats.Set("duration_ms", stopwatch.Elapsed.TotalMilliseconds);

        return new FuzzResult
        {
            Seed = scenario.Seed,
            Passed = passed && assertions.All(assertion => assertion.Verdict != BenchmarkVerdict.Fail),
            FailureFrame = failureFrame,
            FailureReason = failureReason,
            Statistics = stats,
            Assertions = assertions,
            Replay = recorder?.ExportReplay()
        };
    }

    private static void ApplyFuzzInput(BenchmarkHarness harness, FuzzScenario scenario, int frame, ref int inputIndex)
    {
        while (inputIndex + 1 < scenario.InputFrames.Count && scenario.InputFrames[inputIndex + 1].Tick <= frame)
        {
            inputIndex++;
        }

        FuzzInputFrame frameInput = scenario.InputFrames.Count > 0
            ? scenario.InputFrames[Math.Min(inputIndex, scenario.InputFrames.Count - 1)]
            : new FuzzInputFrame();

        PlayerInputState input = new(
            frameInput.Horizontal,
            frameInput.Jump,
            frameInput.Respawn,
            frameInput.FastFall,
            frameInput.Pull,
            null);
        harness.ApplyUniformInput(input);
    }
}
