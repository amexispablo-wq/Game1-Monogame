#nullable enable
using System;
using System.Collections.Generic;
using ColorBlocks.Replay;

namespace ColorBlocks;

/// <summary>
/// Uploads a local official best replay to Steam. Shared by GameScene (on new PB)
/// and LevelSelect (repair/sync when local best beats the peeked WR).
/// </summary>
public static class SteamBestReplayUploader
{
    /// <param name="steamWorldRecordSeconds">
    /// When set (Level Select catch-up), disk-PB recovery only uploads if that disk
    /// time still beats the peeked Steam WR.
    /// </param>
    public static void TryUpload(
        ColorBlocksGame game,
        string levelId,
        int playerCount,
        float timeSeconds,
        IReadOnlyList<ulong>? steamIds = null,
        float? steamWorldRecordSeconds = null,
        Action<bool>? onComplete = null)
    {
        if (!SteamLeaderboardService.SupportsLeaderboards(levelId)
            || !game.SteamLeaderboards.IsAvailable)
        {
            onComplete?.Invoke(false);
            return;
        }

        int clampedPlayers = SteamLeaderboardService.ClampPlayerCount(playerCount);
        string replayPath = ReplayStorage.GetBestReplayPath(levelId, clampedPlayers);
        if (!TryResolveUploadableTime(
                levelId,
                clampedPlayers,
                replayPath,
                timeSeconds,
                steamWorldRecordSeconds,
                out float uploadTime,
                out string resolveReason))
        {
            DiagnosticsLog.Info("SteamLeaderboard", $"Skip upload — {resolveReason}");
            onComplete?.Invoke(false);
            return;
        }

        if (!LeaderboardSanity.TryValidateUpload(
                levelId, uploadTime, clampedPlayers, replayPath, out string sanityReason))
        {
            DiagnosticsLog.Info("SteamLeaderboard", $"Skip upload — {sanityReason}");
            onComplete?.Invoke(false);
            return;
        }

        int levelVersion = LevelLibrary.GetLevel(levelId)?.Version ?? 1;
        IReadOnlyList<ulong> ids = steamIds ?? CollectPartySteamIds(game);
        int scoreUnits = BestTimeStorage.ToLeaderboardScore(uploadTime);
        string boardName = SteamLeaderboardService.GetLeaderboardName(levelId, levelVersion, clampedPlayers);

        DiagnosticsLog.Info(
            "SteamLeaderboard",
            $"Upload start board='{boardName}' time={uploadTime:0.####}s score={scoreUnits}");

        game.SteamReplays.ShareReplayFile(
            replayPath,
            SteamReplayService.GetRemoteReplayName(levelId, clampedPlayers, scoreUnits),
            ugcHandle =>
            {
                if (ugcHandle == 0)
                {
                    DiagnosticsLog.Info(
                        "SteamLeaderboard",
                        $"Share returned UGC=0 for '{boardName}' — uploading score without ghost attachment");
                }

                game.SteamLeaderboards.UploadRecord(
                    new SteamLeaderboardRecord
                    {
                        LevelId = levelId,
                        LevelVersion = levelVersion,
                        TimeSeconds = uploadTime,
                        PlayerCount = clampedPlayers,
                        SteamIds = ids,
                        ReplayUgcHandle = ugcHandle
                    },
                    success =>
                    {
                        if (success)
                        {
                            SteamGhostService.InvalidateWorldRecordGhost(levelId, clampedPlayers);
                            if (ugcHandle != 0)
                            {
                                game.SteamGhosts.EnsureWorldRecordGhost(levelId, clampedPlayers);
                            }
                        }

                        onComplete?.Invoke(success);
                    });
            });
    }

    /// <summary>
    /// Prefer BestTimes time when the disk replay can be repaired to match.
    /// If BestTimes is ahead of disk (legacy solo-REPLAY desync), fall back to disk
    /// OfficialBestTime when that still beats the peeked Steam WR (or board empty).
    /// </summary>
    private static bool TryResolveUploadableTime(
        string levelId,
        int playerCount,
        string replayPath,
        float preferredTimeSeconds,
        float? steamWorldRecordSeconds,
        out float uploadTime,
        out string reason)
    {
        uploadTime = BestTimeStorage.RoundToTenThousandths(preferredTimeSeconds);
        reason = string.Empty;

        if (ReplayFileSerializer.TryRepairDurationMismatch(replayPath, uploadTime, out reason))
        {
            return true;
        }

        string preferredFailReason = reason;
        if (!ReplayStorage.TryLoadBestReplay(levelId, playerCount, out ReplayFile diskReplay))
        {
            reason = preferredFailReason;
            return false;
        }

        float diskTime = BestTimeStorage.RoundToTenThousandths(diskReplay.Metadata.OfficialBestTime);
        if (diskTime <= 0f)
        {
            reason = preferredFailReason;
            return false;
        }

        // BestTimes already matches disk — nothing else to try.
        if (BestTimeStorage.ToLeaderboardScore(diskTime)
            == BestTimeStorage.ToLeaderboardScore(uploadTime))
        {
            reason = preferredFailReason;
            return false;
        }

        if (!ReplayFileSerializer.TryRepairDurationMismatch(replayPath, diskTime, out string diskRepairReason))
        {
            reason =
                $"BestTimes {uploadTime:0.####}s ahead of disk replay; disk repair failed — {diskRepairReason}";
            return false;
        }

        if (steamWorldRecordSeconds is float wr
            && BestTimeStorage.ToLeaderboardScore(diskTime)
                >= BestTimeStorage.ToLeaderboardScore(wr))
        {
            reason =
                $"BestTimes {uploadTime:0.####}s has no matching replay (disk={diskTime:0.####}s); "
                + "disk PB does not beat Steam WR — re-run PB to publish";
            return false;
        }

        DiagnosticsLog.Info(
            "SteamLeaderboard",
            $"Recover upload with disk PB {diskTime:0.####}s (BestTimes {uploadTime:0.####}s had no matching replay)");
        uploadTime = diskTime;
        reason = string.Empty;
        return true;
    }

    private static List<ulong> CollectPartySteamIds(ColorBlocksGame game)
    {
        var steamIds = new List<ulong>();
        foreach (PartyMember member in game.Party.Members)
        {
            if (member.OwningSteamId != 0 && !steamIds.Contains(member.OwningSteamId))
            {
                steamIds.Add(member.OwningSteamId);
            }
        }

        return steamIds;
    }
}
