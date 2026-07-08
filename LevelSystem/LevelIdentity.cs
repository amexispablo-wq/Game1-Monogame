#nullable enable
using System;

namespace ColorBlocks;

public static class LevelIdentity
{
    public const char Separator = ':';

    public static string Compose(LevelSource source, string fileStem)
    {
        if (string.IsNullOrWhiteSpace(fileStem))
        {
            throw new ArgumentException("Level file stem is required.", nameof(fileStem));
        }

        return $"{ToPrefix(source)}{Separator}{fileStem}";
    }

    public static bool TryParse(string levelId, out LevelSource source, out string fileStem)
    {
        source = LevelSource.Local;
        fileStem = string.Empty;

        if (string.IsNullOrWhiteSpace(levelId))
        {
            return false;
        }

        int separatorIndex = levelId.IndexOf(Separator);
        if (separatorIndex > 0 && separatorIndex < levelId.Length - 1)
        {
            string prefix = levelId[..separatorIndex];
            if (TryParsePrefix(prefix, out source))
            {
                fileStem = levelId[(separatorIndex + 1)..];
                return !string.IsNullOrWhiteSpace(fileStem);
            }
        }

        source = LevelSource.Local;
        fileStem = levelId;
        return true;
    }

    public static LevelSource GetSource(string levelId)
    {
        return TryParse(levelId, out LevelSource source, out _) ? source : LevelSource.Local;
    }

    public static string NormalizeLegacyId(string legacyLevelId)
    {
        if (TryParse(legacyLevelId, out _, out _))
        {
            return legacyLevelId;
        }

        if (legacyLevelId.Equals("level_1", StringComparison.OrdinalIgnoreCase))
        {
            return Compose(LevelSource.Official, "Level01");
        }

        if (legacyLevelId.Equals("level_2", StringComparison.OrdinalIgnoreCase))
        {
            return Compose(LevelSource.Official, "Level02");
        }

        return Compose(LevelSource.Local, legacyLevelId);
    }

    public static string ToFileSafeStem(string fileStem)
    {
        foreach (char invalid in System.IO.Path.GetInvalidFileNameChars())
        {
            fileStem = fileStem.Replace(invalid, '_');
        }

        return fileStem.Trim();
    }

    private static string ToPrefix(LevelSource source) =>
        source switch
        {
            LevelSource.Official => "official",
            LevelSource.Local => "local",
            LevelSource.Workshop => "workshop",
            _ => "local"
        };

    private static bool TryParsePrefix(string prefix, out LevelSource source)
    {
        switch (prefix.ToLowerInvariant())
        {
            case "official":
                source = LevelSource.Official;
                return true;
            case "local":
                source = LevelSource.Local;
                return true;
            case "workshop":
                source = LevelSource.Workshop;
                return true;
            default:
                source = LevelSource.Local;
                return false;
        }
    }
}
