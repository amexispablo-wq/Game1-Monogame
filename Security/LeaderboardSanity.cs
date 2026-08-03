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

    /// <summary>
    /// Min wall/sim ratio. Below = fast-forward speedhack.
    /// Wide band avoids false rejects from GC/scheduling noise.
    /// </summary>
    public const float MinActiveWallRatio = 0.65f;

    /// <summary>
    /// Max wall/sim ratio. Above = slow-mo speedhack (CE).
    /// Wide band avoids false rejects from brief focus loss edge cases.
    /// </summary>
    public const float MaxActiveWallRatio = 1.50f;

    /// <summary>Frozen ElapsedTime for this many running frames while moving → reject.</summary>
    public const int TimerFreezeFrameThreshold = 30;

    /// <summary>Min player movement (px) between frames to count as "moving" for freeze detect.</summary>
    public const float TimerFreezeMoveEpsilon = 0.5f;

    /// <summary>Allow tiny float noise when checking ElapsedTime monotonicity.</summary>
    public const float TimerMonotonicEpsilon = 1e-4f;

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

        ReplayData trimmed = ReplayFileSerializer.TrimToLastTimerRun(replay.Data);
        if (trimmed.Frames.Length == 0)
        {
            reason = "Best-replay has no frames after trim.";
            return false;
        }

        ReplayFrameSnapshot last = trimmed.Frames[^1];
        if (!last.Timer.IsComplete)
        {
            reason = "Best-replay does not end in a completed run.";
            return false;
        }

        float finalDelta = MathF.Abs(
            BestTimeStorage.RoundToTenThousandths(last.Timer.FinalTime) - roundedScore);
        if (finalDelta > ReplayDurationToleranceSeconds)
        {
            reason =
                $"Replay FinalTime {last.Timer.FinalTime:F4}s diverges from score {roundedScore:F4}s";
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

        if (!TryValidateActiveWallRatio(replay.Metadata.ActiveWallSeconds, roundedScore, out reason))
        {
            return false;
        }

        if (!TryValidateTimerIntegrity(trimmed, out reason))
        {
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// Compares focused wall-clock to sim score. <paramref name="activeWallSeconds"/> ≤ 0
    /// skips the check (legacy replays / fail-open).
    /// </summary>
    public static bool TryValidateActiveWallRatio(
        float activeWallSeconds,
        float scoreSeconds,
        out string reason)
    {
        reason = string.Empty;

        if (!float.IsFinite(activeWallSeconds) || activeWallSeconds <= 0f)
        {
            return true;
        }

        if (!float.IsFinite(scoreSeconds) || scoreSeconds <= 0f)
        {
            reason = $"Invalid score for wall-clock check: {scoreSeconds}";
            return false;
        }

        float ratio = activeWallSeconds / scoreSeconds;
        if (ratio < MinActiveWallRatio || ratio > MaxActiveWallRatio)
        {
            reason =
                $"Active wall-clock {activeWallSeconds:F3}s vs score {scoreSeconds:F3}s " +
                $"(ratio {ratio:F3}) outside [{MinActiveWallRatio:F2}..{MaxActiveWallRatio:F2}]";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Timer monotonicity + freeze detect on trimmed replay frames.
    /// Does not inspect velocity/jump (legitimate rope/launch can exceed move caps).
    /// </summary>
    public static bool TryValidateTimerIntegrity(ReplayData trimmed, out string reason)
    {
        reason = string.Empty;
        ReplayFrameSnapshot[] frames = trimmed.Frames;
        if (frames.Length == 0)
        {
            reason = "Timer integrity: empty replay.";
            return false;
        }

        float previousElapsed = frames[0].Timer.ElapsedTime;
        int freezeStreak = 0;

        for (int i = 1; i < frames.Length; i++)
        {
            ReplayFrameSnapshot prev = frames[i - 1];
            ReplayFrameSnapshot cur = frames[i];

            if (!cur.Timer.IsRunning)
            {
                freezeStreak = 0;
                previousElapsed = cur.Timer.ElapsedTime;
                continue;
            }

            float elapsed = cur.Timer.ElapsedTime;
            if (elapsed + TimerMonotonicEpsilon < previousElapsed)
            {
                reason =
                    $"Timer integrity: ElapsedTime decreased at frame {i} " +
                    $"({previousElapsed:F4} → {elapsed:F4})";
                return false;
            }

            bool elapsedStuck = MathF.Abs(elapsed - previousElapsed) <= TimerMonotonicEpsilon;
            bool tickAdvanced = cur.Tick > prev.Tick;
            bool playerMoved = AnyPlayerMoved(prev, cur, TimerFreezeMoveEpsilon);

            if (elapsedStuck && tickAdvanced && playerMoved)
            {
                freezeStreak++;
                if (freezeStreak >= TimerFreezeFrameThreshold)
                {
                    reason =
                        $"Timer integrity: ElapsedTime frozen for {freezeStreak} frames " +
                        $"while players moved (frame {i})";
                    return false;
                }
            }
            else
            {
                freezeStreak = 0;
            }

            previousElapsed = elapsed;
        }

        return true;
    }

    private static bool AnyPlayerMoved(
        ReplayFrameSnapshot prev,
        ReplayFrameSnapshot cur,
        float epsilon)
    {
        PlayerSnapshot[] prevPlayers = prev.Players;
        PlayerSnapshot[] curPlayers = cur.Players;
        int count = Math.Min(prevPlayers.Length, curPlayers.Length);
        float epsSq = epsilon * epsilon;

        for (int i = 0; i < count; i++)
        {
            float dx = curPlayers[i].Position.X - prevPlayers[i].Position.X;
            float dy = curPlayers[i].Position.Y - prevPlayers[i].Position.Y;
            if (dx * dx + dy * dy > epsSq)
            {
                return true;
            }
        }

        return false;
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
