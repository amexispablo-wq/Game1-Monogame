#nullable enable
using System;
using System.Collections.Generic;
using ColorBlocks.Replay;
using Microsoft.Xna.Framework;

namespace ColorBlocks.Developer.GameplayBenchmark;

public static class BenchmarkSnapshotComparer
{
    public static List<BenchmarkAssertion> CompareSnapshots(
        GameSnapshot live,
        ReplayFrameSnapshot recorded,
        float positionTolerance,
        float ropeTolerance)
    {
        List<BenchmarkAssertion> assertions = new();
        float maxPositionError = 0f;
        float maxRopeError = 0f;

        int playerCount = Math.Min(live.Players.Count, recorded.Players.Length);
        for (int i = 0; i < playerCount; i++)
        {
            Vector2 livePos = live.Players[i].Position.ToVector2();
            Vector2 recordedPos = recorded.Players[i].Position.ToVector2();
            float error = Vector2.Distance(livePos, recordedPos);
            maxPositionError = MathF.Max(maxPositionError, error);
        }

        int ropeCount = Math.Min(live.Ropes.Count, recorded.Ropes.Length);
        for (int i = 0; i < ropeCount; i++)
        {
            RopeSnapshot liveRope = live.Ropes[i];
            RopeSnapshot recordedRope = recorded.Ropes[i];
            float tensionError = MathF.Abs(liveRope.Tension - recordedRope.Tension);
            maxRopeError = MathF.Max(maxRopeError, tensionError);

            int nodeCount = Math.Min(liveRope.NodePositions.Count, recordedRope.NodePositions.Count);
            for (int n = 0; n < nodeCount; n++)
            {
                float nodeError = Vector2.Distance(
                    liveRope.NodePositions[n].ToVector2(),
                    recordedRope.NodePositions[n].ToVector2());
                maxRopeError = MathF.Max(maxRopeError, nodeError);
            }
        }

        long tickError = Math.Abs(live.Tick - recorded.Tick);
        assertions.Add(maxPositionError <= positionTolerance
            ? BenchmarkAssertion.Pass("replay.position", "Replay position within tolerance.", maxPositionError)
            : BenchmarkAssertion.Fail("replay.position", "Replay position diverged.", maxPositionError, positionTolerance, positionTolerance));

        assertions.Add(maxRopeError <= ropeTolerance
            ? BenchmarkAssertion.Pass("replay.rope", "Replay rope within tolerance.", maxRopeError)
            : BenchmarkAssertion.Fail("replay.rope", "Replay rope diverged.", maxRopeError, ropeTolerance, ropeTolerance));

        assertions.Add(tickError == 0
            ? BenchmarkAssertion.Pass("replay.tick", "Replay tick aligned.")
            : BenchmarkAssertion.Fail("replay.tick", "Replay tick mismatch.", tickError));

        return assertions;
    }
}
