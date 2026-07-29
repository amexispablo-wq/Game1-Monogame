#nullable enable
using System;
using System.IO;
using ColorBlocks.Replay;

namespace ColorBlocks;

/// <summary>Client-side sanity gate before Steam leaderboard upload.</summary>
public static class LeaderboardSanity
{
    public const float MinUploadTimeSeconds = 0.50f;
    public const float MaxUploadTimeSeconds = 86_400f;
    public const float ReplayDurationToleranceSeconds = 0.05f;

    public static bool TryValidateUpload(
        string levelId,
        float timeSeconds,
        int playerCount,
        string replayPath,
        out string reason)
    {
        reason = string.Empty;

        if (!float.IsFinite(timeSeconds)
            || timeSeconds < MinUploadTimeSeconds
            || timeSeconds > MaxUploadTimeSeconds)
        {
            reason = $"Time {timeSeconds}s outside upload bounds [{MinUploadTimeSeconds}..{MaxUploadTimeSeconds}]";
            return false;
        }

        LevelSource source = LevelIdentity.GetSource(levelId);
        if (source == LevelSource.Official)
        {
            LevelMetadata? metadata = LevelLibrary.GetLevel(levelId);
            if (metadata is not null
                && !OfficialLevelManifest.VerifyLevelFile(levelId, metadata.FilePath, out string manifestReason))
            {
                reason = manifestReason;
                return false;
            }
        }

        if (!File.Exists(replayPath))
        {
            reason = "Official PB upload requires a best-replay file.";
            return false;
        }

        ReplayFile? replay = ReplayFileSerializer.TryLoad(replayPath, invalidateOnHashMismatch: false);
        if (replay is null)
        {
            reason = "Best-replay failed integrity checks.";
            return false;
        }

        if (!string.IsNullOrEmpty(replay.Metadata.DataChecksum)
            && !ReplayFileSerializer.VerifyDataChecksum(replay))
        {
            reason = "Best-replay data checksum mismatch.";
            return false;
        }

        float roundedScore = BestTimeStorage.RoundToTenThousandths(timeSeconds);
        float durationDelta = MathF.Abs(replay.Metadata.DurationSeconds - roundedScore);
        if (durationDelta > ReplayDurationToleranceSeconds)
        {
            reason =
                $"Replay duration {replay.Metadata.DurationSeconds:F4}s diverges from score {roundedScore:F4}s";
            return false;
        }

        if (!LevelContentHash.MatchesCurrentLevel(levelId, replay.Metadata.LevelContentHash))
        {
            reason = "Replay level content hash does not match current level.";
            return false;
        }

        if (replay.Metadata.PlayerCount != BestTimeStorage.ClampPlayerCount(playerCount))
        {
            reason = "Replay player count does not match upload player count.";
            return false;
        }

        return true;
    }

    public static bool IsSuspiciousEntry(SteamLeaderboardEntry entry)
    {
        if (entry.TimeSeconds < MinUploadTimeSeconds || entry.TimeSeconds > MaxUploadTimeSeconds)
        {
            return true;
        }

        if (entry.CompletionDateUtc != default && entry.CompletionDateUtc > DateTime.UtcNow.AddHours(1))
        {
            return true;
        }

        if (string.IsNullOrEmpty(entry.GameVersion) && entry.BuildGuidPrefix == 0 && entry.ReplayId == 0)
        {
            // Empty details often means a raw score without schema — flag but keep.
            return entry.TimeSeconds < 1f;
        }

        return false;
    }
}
