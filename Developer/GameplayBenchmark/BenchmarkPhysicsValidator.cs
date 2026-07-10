#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace ColorBlocks.Developer.GameplayBenchmark;

public static class BenchmarkPhysicsValidator
{
    public static List<BenchmarkAssertion> ValidateSimulation(GameSimulation simulation, Level level)
    {
        List<BenchmarkAssertion> assertions = new();

        foreach (Player player in simulation.Players)
        {
            if (!IsFinite(player.Position) || !IsFinite(player.Velocity))
            {
                assertions.Add(BenchmarkAssertion.Fail("player.finite", $"Player {player.PlayerIndex + 1} has non-finite state."));
            }

            if (IsOutsideWorld(player.Position, level.WorldSize))
            {
                assertions.Add(BenchmarkAssertion.Fail("player.bounds", $"Player {player.PlayerIndex + 1} left world bounds."));
            }
        }

        foreach (Rope rope in simulation.Ropes)
        {
            if (rope.CurrentPathLength < 0f || rope.TargetRestLength < 0f)
            {
                assertions.Add(BenchmarkAssertion.Fail("rope.length", "Rope length became negative."));
            }

            if (rope.LastEndpointForce < 0f || !float.IsFinite(rope.LastEndpointForce))
            {
                assertions.Add(BenchmarkAssertion.Fail("rope.force", "Rope force invalid."));
            }

            foreach (RopeNode node in rope.Nodes)
            {
                if (!IsFinite(node.Position))
                {
                    assertions.Add(BenchmarkAssertion.Fail("rope.node", "Rope node position is non-finite."));
                    break;
                }
            }
        }

        if (assertions.Count == 0)
        {
            assertions.Add(BenchmarkAssertion.Pass("physics.valid", "Physics state remained valid."));
        }

        return assertions;
    }

    private static bool IsFinite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);

    private static bool IsOutsideWorld(Vector2 position, Point worldSize)
    {
        const float margin = 512f;
        return position.X < -margin
            || position.Y < -margin
            || position.X > worldSize.X + margin
            || position.Y > worldSize.Y + margin;
    }
}
