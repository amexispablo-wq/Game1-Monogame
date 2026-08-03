#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using ColorBlocks.Replay;

namespace ColorBlocks.Developer.GameplayBenchmark.Scenarios;

/// <summary>
/// Upload-only LeaderboardSanity checks: wall-clock ratio + timer freeze/monotonicity.
/// No physics velocity caps (ropes/launches are legitimate).
/// </summary>
public sealed class LeaderboardSanityBenchmark : BenchmarkScenario
{
    public override string Id => "security.leaderboard_sanity";
    public override string Name => "Leaderboard Sanity (time integrity)";
    public override BenchmarkCategory Category => BenchmarkCategory.Replay;

    public override BenchmarkResult Run(BenchmarkContext context)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        List<BenchmarkAssertion> assertions = new();
        BenchmarkStatistics stats = new();

        AssertWallRatio(assertions);
        AssertTimerIntegrity(assertions);

        stopwatch.Stop();
        return new BenchmarkResult(
            Id,
            Name,
            Category,
            BenchmarkVerdict.Pass,
            stopwatch.Elapsed,
            assertions,
            stats);
    }

    private static void AssertWallRatio(List<BenchmarkAssertion> assertions)
    {
        // Legacy / absent → fail-open.
        bool legacyOk = LeaderboardSanity.TryValidateActiveWallRatio(0f, 10f, out string legacyReason);
        assertions.Add(legacyOk
            ? BenchmarkAssertion.Pass("wall.legacy_skip", "ActiveWallSeconds=0 skips wall check.")
            : BenchmarkAssertion.Fail("wall.legacy_skip", legacyReason));

        bool inBand = LeaderboardSanity.TryValidateActiveWallRatio(10.5f, 10f, out string inBandReason);
        assertions.Add(inBand
            ? BenchmarkAssertion.Pass("wall.in_band", "Ratio ~1.05 accepted.", 1.05f)
            : BenchmarkAssertion.Fail("wall.in_band", inBandReason));

        bool slowMo = LeaderboardSanity.TryValidateActiveWallRatio(20f, 10f, out string slowReason);
        assertions.Add(!slowMo
            ? BenchmarkAssertion.Pass("wall.slowmo_reject", "Slow-mo ratio rejected.", 2f)
            : BenchmarkAssertion.Fail("wall.slowmo_reject", "Expected reject for wall/score=2."));

        bool fastFwd = LeaderboardSanity.TryValidateActiveWallRatio(5f, 10f, out string fastReason);
        assertions.Add(!fastFwd
            ? BenchmarkAssertion.Pass("wall.fastfwd_reject", "Fast-forward ratio rejected.", 0.5f)
            : BenchmarkAssertion.Fail("wall.fastfwd_reject", "Expected reject for wall/score=0.5."));
    }

    private static void AssertTimerIntegrity(List<BenchmarkAssertion> assertions)
    {
        float dt = 1f / 60f;

        ReplayData normal = BuildRun(
            frameCount: 90,
            elapsedAt: i => i * dt,
            movePerFrame: 2f);
        bool normalOk = LeaderboardSanity.TryValidateTimerIntegrity(normal, out string normalReason);
        assertions.Add(normalOk
            ? BenchmarkAssertion.Pass("timer.monotonic_ok", "Normal advancing timer accepted.")
            : BenchmarkAssertion.Fail("timer.monotonic_ok", normalReason));

        ReplayData frozen = BuildRun(
            frameCount: LeaderboardSanity.TimerFreezeFrameThreshold + 5,
            elapsedAt: _ => 1f,
            movePerFrame: 3f);
        bool freezeRejected = !LeaderboardSanity.TryValidateTimerIntegrity(frozen, out _);
        assertions.Add(freezeRejected
            ? BenchmarkAssertion.Pass(
                "timer.freeze_reject",
                "Frozen ElapsedTime while moving rejected.",
                LeaderboardSanity.TimerFreezeFrameThreshold)
            : BenchmarkAssertion.Fail("timer.freeze_reject", "Expected freeze reject."));

        // Frozen timer but player idle → must NOT reject (stand still is fine).
        ReplayData idleFrozen = BuildRun(
            frameCount: LeaderboardSanity.TimerFreezeFrameThreshold + 5,
            elapsedAt: _ => 1f,
            movePerFrame: 0f);
        bool idleOk = LeaderboardSanity.TryValidateTimerIntegrity(idleFrozen, out string idleReason);
        assertions.Add(idleOk
            ? BenchmarkAssertion.Pass("timer.idle_freeze_ok", "Idle frames with stuck elapsed accepted.")
            : BenchmarkAssertion.Fail("timer.idle_freeze_ok", idleReason));

        ReplayData decreased = BuildRun(
            frameCount: 10,
            elapsedAt: i => i < 5 ? i * dt : (9 - i) * dt,
            movePerFrame: 1f);
        bool decreaseRejected = !LeaderboardSanity.TryValidateTimerIntegrity(decreased, out _);
        assertions.Add(decreaseRejected
            ? BenchmarkAssertion.Pass("timer.decrease_reject", "Decreasing ElapsedTime rejected.")
            : BenchmarkAssertion.Fail("timer.decrease_reject", "Expected decrease reject."));
    }

    private static ReplayData BuildRun(
        int frameCount,
        Func<int, float> elapsedAt,
        float movePerFrame)
    {
        var frames = new ReplayFrameSnapshot[frameCount];
        for (int i = 0; i < frameCount; i++)
        {
            float x = i * movePerFrame;
            bool last = i == frameCount - 1;
            float elapsed = elapsedAt(i);
            frames[i] = new ReplayFrameSnapshot
            {
                Tick = i,
                Timer = new TimerSnapshot(
                    elapsed,
                    IsRunning: !last,
                    IsComplete: last,
                    FinalTime: last ? elapsed : 0f,
                    NewRecord: false),
                Players =
                [
                    new PlayerSnapshot(
                        NetworkId: 1,
                        OwnerId: 1,
                        PlayerIndex: 0,
                        PlayerId: PlayerId.Player1,
                        Position: new NetworkVector2(x, 0f),
                        Velocity: new NetworkVector2(movePerFrame * 60f, 0f),
                        Acceleration: default,
                        Color: GameColor.Red,
                        State: PlayerState.Normal,
                        IsGrounded: true,
                        IsFrozen: false)
                ]
            };
        }

        return new ReplayData
        {
            Header = new ReplayHeader
            {
                LevelId = "benchmark_sanity",
                TicksPerSecond = 60
            },
            Frames = frames
        };
    }
}
