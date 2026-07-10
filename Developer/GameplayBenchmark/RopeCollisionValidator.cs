#nullable enable
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace ColorBlocks.Developer.GameplayBenchmark;

internal static class RopeCollisionValidator
{
    private const float NodeCollisionRadius = 5f;

    public static int CountSegmentPenetrations(Rope rope, IReadOnlyList<Platform> platforms)
    {
        if (rope.GameplayMode != RopeGameplayMode.ColoredPhysics || platforms.Count == 0)
        {
            return 0;
        }

        int penetrations = 0;
        foreach (RopeConstraint constraint in rope.Constraints)
        {
            Vector2 start = constraint.A.Position;
            Vector2 end = constraint.B.Position;
            foreach (Platform platform in platforms)
            {
                if (!CanCollide(rope, platform))
                {
                    continue;
                }

                if (CollisionHelper.SegmentPenetratesPlatformInterior(
                    start,
                    end,
                    platform.Bounds,
                    NodeCollisionRadius))
                {
                    penetrations++;
                }
            }
        }

        return penetrations;
    }

    public static int CountNodePenetrations(Rope rope, IReadOnlyList<Platform> platforms)
    {
        if (rope.GameplayMode != RopeGameplayMode.ColoredPhysics || platforms.Count == 0)
        {
            return 0;
        }

        int penetrations = 0;
        Vector2 nodeSize = new(NodeCollisionRadius * 2f);
        foreach (RopeNode node in rope.Nodes)
        {
            if (node.IsPinned)
            {
                continue;
            }

            Vector2 probe = node.Position - new Vector2(NodeCollisionRadius);
            foreach (Platform platform in platforms)
            {
                if (!CanCollide(rope, platform))
                {
                    continue;
                }

                if (CollisionHelper.Intersects(probe, nodeSize, platform.Bounds))
                {
                    penetrations++;
                }
            }
        }

        return penetrations;
    }

    private static bool CanCollide(Rope rope, Platform platform)
    {
        return platform.PlatformColor switch
        {
            GameColor.Red => rope.StartPlayer.PlayerColor == GameColor.Red || rope.EndPlayer.PlayerColor == GameColor.Red,
            GameColor.Green => rope.StartPlayer.PlayerColor == GameColor.Green || rope.EndPlayer.PlayerColor == GameColor.Green,
            GameColor.Blue => rope.StartPlayer.PlayerColor == GameColor.Blue || rope.EndPlayer.PlayerColor == GameColor.Blue,
            _ => false
        };
    }
}
