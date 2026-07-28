#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorBlocks;

public enum LevelValidationProfile
{
    /// <summary>Workshop publish/download — tightest limits.</summary>
    Strict,
    /// <summary>Normal load / listing — reject grief/crash shapes.</summary>
    Playable,
    /// <summary>Editor save — extreme bounds only.</summary>
    Editor
}

public sealed class LevelValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; } = new();

    public string Summary => Errors.Count == 0
        ? "OK"
        : string.Join("; ", Errors.Take(5));
}

/// <summary>Central schema/bounds gate for level JSON before play, list, or Workshop I/O.</summary>
public static class LevelValidator
{
    public const int MaxNameLength = 80;
    public const int MaxAuthorLength = 64;
    public const int MaxMusicIdLength = 64;
    public const int MaxWorkshopIdLength = 32;
    public const int MaxSignTextLength = 500;
    public const int MaxCoordinate = 50_000;
    public const int MaxWorldWidth = 20_000;
    public const int MaxWorldHeight = 20_000;
    public const long MaxLevelFileBytes = 2 * 1024 * 1024;

    public static LevelValidationResult Validate(LevelData data, LevelValidationProfile profile)
    {
        var result = new LevelValidationResult();
        Limits limits = LimitsFor(profile);

        ValidateName(data.Name, limits, result);
        ValidateString(data.Author, "author", MaxAuthorLength, result);
        ValidateString(data.MusicId, "musicId", MaxMusicIdLength, result);
        ValidateString(data.WorkshopId, "workshopId", MaxWorkshopIdLength, result);
        ValidateString(data.OwnerSteamId, "ownerSteamId", 32, result);
        ValidateString(data.DownloadedVersion, "downloadedVersion", 32, result);

        if (data.Version < 1 || data.Version > 9999)
        {
            result.Errors.Add($"version {data.Version} out of range 1..9999");
        }

        if (data.Platforms.Count > limits.MaxPlatforms)
        {
            result.Errors.Add($"platforms count {data.Platforms.Count} > {limits.MaxPlatforms}");
        }

        if (data.Goals.Count > limits.MaxGoals)
        {
            result.Errors.Add($"goals count {data.Goals.Count} > {limits.MaxGoals}");
        }

        if (data.CheckpointFlags.Count > limits.MaxCheckpoints)
        {
            result.Errors.Add($"checkpointFlags count {data.CheckpointFlags.Count} > {limits.MaxCheckpoints}");
        }

        if (data.LaunchPads.Count > limits.MaxLaunchPads)
        {
            result.Errors.Add($"launchPads count {data.LaunchPads.Count} > {limits.MaxLaunchPads}");
        }

        if (data.PowerUps.Count > limits.MaxPowerUps)
        {
            result.Errors.Add($"powerUps count {data.PowerUps.Count} > {limits.MaxPowerUps}");
        }

        if (data.Signs.Count > limits.MaxSigns)
        {
            result.Errors.Add($"signs count {data.Signs.Count} > {limits.MaxSigns}");
        }

        if (profile == LevelValidationProfile.Strict && data.Goals.Count < 1)
        {
            result.Errors.Add("at least one goal required");
        }

        ValidateCoord("playerSpawn.x", data.PlayerSpawn.X, result);
        ValidateCoord("playerSpawn.y", data.PlayerSpawn.Y, result);

        foreach (PlatformData platform in data.Platforms)
        {
            ValidateRect("platform", platform.X, platform.Y, platform.Width, platform.Height, result);
        }

        foreach (GoalData goal in data.Goals)
        {
            ValidateCoord("goal.x", goal.X, result);
            ValidateCoord("goal.y", goal.Y, result);
        }

        foreach (CheckpointFlagData flag in data.CheckpointFlags)
        {
            ValidateCoord("checkpoint.x", flag.X, result);
            ValidateCoord("checkpoint.y", flag.Y, result);
        }

        foreach (LaunchPadData pad in data.LaunchPads)
        {
            ValidateRect("launchPad", pad.X, pad.Y, pad.Width, pad.Height, result);
            if (pad.LaunchForce < LaunchPad.MinLaunchForce || pad.LaunchForce > LaunchPad.MaxLaunchForce)
            {
                // Soft: FromData clamps; Strict/Playable still flag extremes beyond clamp range.
                if (pad.LaunchForce < 0f || pad.LaunchForce > 10_000f)
                {
                    result.Errors.Add($"launchPad force {pad.LaunchForce} out of range");
                }
            }
        }

        foreach (PowerUpData powerUp in data.PowerUps)
        {
            ValidateRect("powerUp", powerUp.X, powerUp.Y, powerUp.Width, powerUp.Height, result);
            if (powerUp.DurationSeconds < 0f || powerUp.DurationSeconds > PowerUp.MaxDurationSeconds * 2f)
            {
                result.Errors.Add($"powerUp duration {powerUp.DurationSeconds} out of range");
            }

            if (powerUp.Multiplier < 0f || powerUp.Multiplier > PowerUp.MaxMultiplier * 2f)
            {
                result.Errors.Add($"powerUp multiplier {powerUp.Multiplier} out of range");
            }
        }

        foreach (SignData sign in data.Signs)
        {
            ValidateCoord("sign.x", sign.X, result);
            ValidateCoord("sign.y", sign.Y, result);
            if (sign.Text.Length > MaxSignTextLength)
            {
                result.Errors.Add($"sign text length {sign.Text.Length} > {MaxSignTextLength}");
            }

            if (ContainsControlChars(sign.Text))
            {
                result.Errors.Add("sign text contains control characters");
            }
        }

        if (data.LavaLine is LavaLineData lava)
        {
            ValidateCoord("lavaLine.surfaceY", lava.SurfaceY, result);
            if (lava.RiseSpeed < 0f || lava.RiseSpeed > 5000f)
            {
                result.Errors.Add($"lava riseSpeed {lava.RiseSpeed} out of range");
            }
        }

        EstimateWorldSize(data, result);
        return result;
    }

    private static void ValidateName(string name, Limits limits, LevelValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            if (limits.RequireName)
            {
                result.Errors.Add("name required");
            }

            return;
        }

        if (name.Length > MaxNameLength)
        {
            result.Errors.Add($"name length {name.Length} > {MaxNameLength}");
        }

        if (ContainsControlChars(name))
        {
            result.Errors.Add("name contains control characters");
        }
    }

    private static void ValidateString(string value, string field, int maxLength, LevelValidationResult result)
    {
        if (value.Length > maxLength)
        {
            result.Errors.Add($"{field} length {value.Length} > {maxLength}");
        }
    }

    private static void ValidateCoord(string field, float value, LevelValidationResult result)
    {
        if (!float.IsFinite(value) || MathF.Abs(value) > MaxCoordinate)
        {
            result.Errors.Add($"{field}={value} out of ±{MaxCoordinate}");
        }
    }

    private static void ValidateRect(
        string kind,
        int x,
        int y,
        int width,
        int height,
        LevelValidationResult result)
    {
        ValidateCoord($"{kind}.x", x, result);
        ValidateCoord($"{kind}.y", y, result);
        if (width < 0 || height < 0 || width > MaxWorldWidth || height > MaxWorldHeight)
        {
            result.Errors.Add($"{kind} size {width}x{height} invalid");
        }
    }

    private static void EstimateWorldSize(LevelData data, LevelValidationResult result)
    {
        int minX = 0;
        int minY = 0;
        int maxX = 1280;
        int maxY = 720;

        void Expand(int x, int y, int w, int h)
        {
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x + Math.Max(0, w));
            maxY = Math.Max(maxY, y + Math.Max(0, h));
        }

        foreach (PlatformData p in data.Platforms)
        {
            Expand(p.X, p.Y, p.Width, p.Height);
        }

        foreach (GoalData g in data.Goals)
        {
            Expand(g.X, g.Y, 32, 32);
        }

        Expand((int)data.PlayerSpawn.X, (int)data.PlayerSpawn.Y, 32, 32);

        int width = maxX - minX + 400;
        int height = maxY - minY + 400;
        if (width > MaxWorldWidth || height > MaxWorldHeight)
        {
            result.Errors.Add($"estimated world size {width}x{height} exceeds {MaxWorldWidth}x{MaxWorldHeight}");
        }
    }

    private static bool ContainsControlChars(string text)
    {
        foreach (char c in text)
        {
            if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')
            {
                return true;
            }
        }

        return false;
    }

    private static Limits LimitsFor(LevelValidationProfile profile) =>
        profile switch
        {
            LevelValidationProfile.Strict => new Limits(500, 50, 100, 200, 50, 200, RequireName: true),
            LevelValidationProfile.Playable => new Limits(500, 50, 100, 200, 50, 200, RequireName: false),
            LevelValidationProfile.Editor => new Limits(2000, 200, 400, 400, 100, 500, RequireName: false),
            _ => new Limits(500, 50, 100, 200, 50, 200, RequireName: false)
        };

    private readonly record struct Limits(
        int MaxPlatforms,
        int MaxGoals,
        int MaxCheckpoints,
        int MaxLaunchPads,
        int MaxPowerUps,
        int MaxSigns,
        bool RequireName);
}
