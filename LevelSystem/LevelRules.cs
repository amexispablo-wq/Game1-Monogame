#nullable enable
using System.Collections.Generic;

namespace ColorBlocks;

public static class LevelRules
{
    public static bool SupportsPlayerCount(Level level, int playerCount)
    {
        if (playerCount < 1 || playerCount > PartyManager.MaxMembers)
        {
            return false;
        }

        if (level.AllPlayers)
        {
            return true;
        }

        return playerCount switch
        {
            1 => level.Player1,
            2 => level.Player2,
            3 => level.Player3,
            4 => level.Player4,
            _ => false
        };
    }

    public static IReadOnlyList<int> GetSupportedPlayerCounts(Level level)
    {
        var counts = new List<int>();
        if (level.AllPlayers)
        {
            for (int i = 1; i <= PartyManager.MaxMembers; i++)
            {
                counts.Add(i);
            }

            return counts;
        }

        if (level.Player1) counts.Add(1);
        if (level.Player2) counts.Add(2);
        if (level.Player3) counts.Add(3);
        if (level.Player4) counts.Add(4);
        return counts;
    }

    public static IReadOnlyList<RopeGameplayMode> GetAllowedRopeModes(Level level)
    {
        if (level.AnyRope || (!level.ColoredRope && !level.RegularRope))
        {
            return new[]
            {
                RopeGameplayMode.ColoredPhysics,
                RopeGameplayMode.Neutral
            };
        }

        bool colored = level.ColoredRope;
        bool regular = level.RegularRope;

        if (colored && !regular)
        {
            return new[] { RopeGameplayMode.ColoredPhysics };
        }

        if (regular && !colored)
        {
            return new[] { RopeGameplayMode.Neutral };
        }

        return new[]
        {
            RopeGameplayMode.ColoredPhysics,
            RopeGameplayMode.Neutral
        };
    }

    public static bool IsRopeModeAllowed(Level level, RopeGameplayMode mode)
    {
        foreach (RopeGameplayMode allowed in GetAllowedRopeModes(level))
        {
            if (allowed == mode)
            {
                return true;
            }
        }

        return false;
    }

    public static RopeGameplayMode ClampRopeMode(Level level, RopeGameplayMode preferred)
    {
        IReadOnlyList<RopeGameplayMode> allowed = GetAllowedRopeModes(level);
        foreach (RopeGameplayMode mode in allowed)
        {
            if (mode == preferred)
            {
                return preferred;
            }
        }

        return allowed[0];
    }

    public static bool SupportsLavaRise(Level level) => level.LavaRise;

    public static bool SupportsPlayerCollision(Level level) => level.PlayerCollision;

    /// <summary>
    /// Official runs must match level rules exactly for features,
    /// and use an allowed rope mode / player count.
    /// Turning OFF a level feature or ON a disallowed one → unofficial.
    /// </summary>
    public static bool IsOfficialPlaySettings(
        Level level,
        RopeGameplayMode ropeMode,
        bool lavaRiseEnabled,
        bool playerCollisionEnabled,
        int playerCount)
    {
        if (!SupportsPlayerCount(level, playerCount))
        {
            return false;
        }

        if (!IsRopeModeAllowed(level, ropeMode))
        {
            return false;
        }

        if (lavaRiseEnabled != SupportsLavaRise(level))
        {
            return false;
        }

        if (playerCollisionEnabled != SupportsPlayerCollision(level))
        {
            return false;
        }

        return true;
    }
}
