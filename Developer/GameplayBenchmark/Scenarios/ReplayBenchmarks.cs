#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using ColorBlocks.Replay;
using Microsoft.Xna.Framework;

namespace ColorBlocks.Developer.GameplayBenchmark.Scenarios;

public sealed class ReplayDeterminismBenchmark : BenchmarkScenario
{
    public override string Id => "replay.determinism";
    public override string Name => "Replay Determinism";
    public override BenchmarkCategory Category => BenchmarkCategory.Replay;

    public override BenchmarkResult Run(BenchmarkContext context)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        Level level = BenchmarkLevelFactory.CreateReplayArena();
        List<BenchmarkAssertion> assertions = new();
        BenchmarkStatistics stats = new();

        using BenchmarkHarness recordHarness = context.CreateHarness(level, 2, RopeGameplayMode.ColoredPhysics, levelId: "benchmark_replay");
        ReplayRecorder recorder = new();
        recorder.StartRecording(
            "benchmark_replay",
            RopeGameplayMode.ColoredPhysics,
            lavaRiseEnabled: false,
            recordHarness.Simulation.TickRate.TicksPerSecond,
            recordHarness.Simulation.LavaRiseSpeed,
            recordHarness.Simulation.LavaSurfaceY);

        ScriptedInputSequence sequence = ScriptedInputSequence.CreateWalkJump(240);
        float maxPositionError = 0f;
        float maxRopeError = 0f;
        long maxTickError = 0;

        for (int tick = 0; tick < 240; tick++)
        {
            sequence.Apply(recordHarness, tick);
            recordHarness.Simulation.Advance(recordHarness.Simulation.TickRate.FixedDeltaSeconds, recordHarness.Input);
            recorder.RecordFrame(recordHarness.Simulation, recordHarness.Camera);
        }

        ReplayData? replay = recorder.ExportReplay();
        assertions.Add(replay is not null
            ? BenchmarkAssertion.Pass("replay.record", "Replay recorded successfully.", replay.Frames.Length)
            : BenchmarkAssertion.Fail("replay.record", "Replay export failed."));

        if (replay is null)
        {
            return new BenchmarkResult(Id, Name, Category, BenchmarkVerdict.Fail, stopwatch.Elapsed, assertions, stats);
        }

        using BenchmarkHarness playbackHarness = context.CreateHarness(level, 2, RopeGameplayMode.ColoredPhysics, levelId: "benchmark_replay");
        for (int tick = 0; tick < replay.Frames.Length; tick++)
        {
            sequence.Apply(playbackHarness, tick);
            playbackHarness.Simulation.Advance(playbackHarness.Simulation.TickRate.FixedDeltaSeconds, playbackHarness.Input);
            ReplayFrameSnapshot recorded = replay.Frames[tick];
            GameSnapshot live = playbackHarness.Simulation.LastSnapshot;

            maxTickError = Math.Max(maxTickError, Math.Abs(live.Tick - recorded.Tick));
            int playerCount = Math.Min(live.Players.Count, recorded.Players.Length);
            for (int i = 0; i < playerCount; i++)
            {
                float error = Vector2.Distance(live.Players[i].Position.ToVector2(), recorded.Players[i].Position.ToVector2());
                maxPositionError = MathF.Max(maxPositionError, error);
            }

            int ropeCount = Math.Min(live.Ropes.Count, recorded.Ropes.Length);
            for (int i = 0; i < ropeCount; i++)
            {
                maxRopeError = MathF.Max(maxRopeError, MathF.Abs(live.Ropes[i].Tension - recorded.Ropes[i].Tension));
            }
        }

        stats.Set("max_position_error", maxPositionError);
        stats.Set("max_rope_error", maxRopeError);
        stats.Set("max_tick_error", maxTickError);

        assertions.Add(maxPositionError <= context.Settings.ReplayPositionTolerance
            ? BenchmarkAssertion.Pass("replay.position", "Replay position stayed deterministic.", maxPositionError)
            : BenchmarkAssertion.Fail("replay.position", "Replay position diverged.", maxPositionError, context.Settings.ReplayPositionTolerance, context.Settings.ReplayPositionTolerance));

        assertions.Add(maxRopeError <= context.Settings.ReplayRopeTolerance
            ? BenchmarkAssertion.Pass("replay.rope", "Replay rope stayed deterministic.", maxRopeError)
            : BenchmarkAssertion.Fail("replay.rope", "Replay rope diverged.", maxRopeError, context.Settings.ReplayRopeTolerance, context.Settings.ReplayRopeTolerance));

        assertions.Add(maxTickError == 0
            ? BenchmarkAssertion.Pass("replay.tick", "Replay tick timeline aligned.")
            : BenchmarkAssertion.Fail("replay.tick", "Replay tick timeline diverged.", maxTickError));

        return new BenchmarkResult(Id, Name, Category, BenchmarkVerdict.Pass, stopwatch.Elapsed, assertions, stats);
    }
}

public sealed class GhostDeterminismBenchmark : BenchmarkScenario
{
    public override string Id => "ghost.determinism";
    public override string Name => "Ghost Determinism";
    public override BenchmarkCategory Category => BenchmarkCategory.Ghost;

    public override BenchmarkResult Run(BenchmarkContext context)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        Level level = BenchmarkLevelFactory.CreateReplayArena();
        List<BenchmarkAssertion> assertions = new();
        BenchmarkStatistics stats = new();

        using BenchmarkHarness recordHarness = context.CreateHarness(level, 1, RopeGameplayMode.ColoredPhysics, levelId: "benchmark_ghost");
        ReplayRecorder recorder = new();
        recorder.StartRecording(
            "benchmark_ghost",
            RopeGameplayMode.ColoredPhysics,
            false,
            recordHarness.Simulation.TickRate.TicksPerSecond,
            recordHarness.Simulation.LavaRiseSpeed,
            recordHarness.Simulation.LavaSurfaceY);

        ScriptedInputSequence sequence = ScriptedInputSequence.CreateWalkJump(180);
        for (int tick = 0; tick < 180; tick++)
        {
            sequence.Apply(recordHarness, tick);
            recordHarness.Simulation.Advance(recordHarness.Simulation.TickRate.FixedDeltaSeconds, recordHarness.Input);
            recorder.RecordFrame(recordHarness.Simulation, recordHarness.Camera);
        }

        ReplayData? replay = recorder.ExportReplay();
        if (replay is null)
        {
            assertions.Add(BenchmarkAssertion.Fail("ghost.record", "Could not record ghost source replay."));
            return new BenchmarkResult(Id, Name, Category, BenchmarkVerdict.Fail, stopwatch.Elapsed, assertions);
        }

        ReplayPlayer ghostPlayer = new();
        ghostPlayer.Load(replay, ReplayCameraMode.Recorded, ReplayPlaybackEndMode.Stop);
        using BenchmarkHarness liveHarness = context.CreateHarness(level, 1, RopeGameplayMode.ColoredPhysics, levelId: "benchmark_ghost");

        float maxPositionError = 0f;
        float maxVelocityError = 0f;
        for (int tick = 0; tick < replay.Frames.Length; tick++)
        {
            sequence.Apply(liveHarness, tick);
            liveHarness.Simulation.Advance(liveHarness.Simulation.TickRate.FixedDeltaSeconds, liveHarness.Input);
            ghostPlayer.SeekToFrame(tick);

            if (replay.Frames[tick].Players.Length == 0 || liveHarness.Simulation.Players.Count == 0)
            {
                continue;
            }

            Vector2 ghostPos = replay.Frames[tick].Players[0].Position.ToVector2();
            Vector2 ghostVel = replay.Frames[tick].Players[0].Velocity.ToVector2();
            Vector2 livePos = liveHarness.Simulation.Players[0].Position;
            Vector2 liveVel = liveHarness.Simulation.Players[0].Velocity;
            maxPositionError = MathF.Max(maxPositionError, Vector2.Distance(ghostPos, livePos));
            maxVelocityError = MathF.Max(maxVelocityError, Vector2.Distance(ghostVel, liveVel));
        }

        stats.Set("max_position_error", maxPositionError);
        stats.Set("max_velocity_error", maxVelocityError);
        assertions.Add(maxPositionError <= context.Settings.GhostPositionTolerance
            ? BenchmarkAssertion.Pass("ghost.position", "Ghost playback matched live deterministic run.", maxPositionError)
            : BenchmarkAssertion.Fail("ghost.position", "Ghost playback diverged from live run.", maxPositionError, context.Settings.GhostPositionTolerance, context.Settings.GhostPositionTolerance));

        assertions.Add(maxVelocityError <= context.Settings.GhostVelocityTolerance
            ? BenchmarkAssertion.Pass("ghost.velocity", "Ghost velocity matched live deterministic run.", maxVelocityError)
            : BenchmarkAssertion.Fail("ghost.velocity", "Ghost velocity diverged from live run.", maxVelocityError, context.Settings.GhostVelocityTolerance, context.Settings.GhostVelocityTolerance));

        return new BenchmarkResult(Id, Name, Category, BenchmarkVerdict.Pass, stopwatch.Elapsed, assertions, stats);
    }
}

internal sealed class ScriptedInputSequence
{
    private readonly int _ticks;

    private ScriptedInputSequence(int ticks) => _ticks = ticks;

    public static ScriptedInputSequence CreateWalkJump(int ticks) => new(ticks);

    public void Apply(BenchmarkHarness harness, int tick)
    {
        bool jump = tick == 40 || tick == 120;
        float move = tick < _ticks / 2 ? 1f : -1f;
        harness.ApplyUniformInput(new PlayerInputState(move, jump, false, false, false, null));
    }
}
