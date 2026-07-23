#nullable enable
using System;
using System.IO;
using System.Text.Json;
using ColorBlocks.Replay;

namespace ColorBlocks;

/// <summary>
/// Downloads and caches the current World Record ghost for Official and Workshop
/// levels, per player-count board. The ghost IS the world-record replay file
/// (same format the existing Ghost/Replay systems use); playback goes through
/// the existing GhostPlayer.
/// Cache layout: Ghosts/{Official|Workshop}/{levelId}_WorldRecord_p{n}.replay
/// plus a sidecar meta file storing the source UGC handle, level version, and
/// score. Stale ghosts are refreshed when the leaderboard top handle/score changes.
/// </summary>
public sealed class SteamGhostService
{
    private const int MaxPlayerCounts = 4;

    private sealed class WorldRecordGhostMeta
    {
        public ulong UgcHandle { get; set; }
        public int LevelVersion { get; set; }
        public int PlayerCount { get; set; } = 1;
        public int ScoreCentiseconds { get; set; }
    }

    private readonly SteamLeaderboardService _leaderboards;
    private readonly SteamReplayService _replays;

    public SteamGhostService(SteamLeaderboardService leaderboards, SteamReplayService replays)
    {
        _leaderboards = leaderboards;
        _replays = replays;
    }

    public bool IsAvailable => _replays.IsAvailable;

    public static bool SupportsWorldRecordGhost(string levelId) =>
        LevelIdentity.GetSource(levelId) != LevelSource.Local;

    public static string GetWorldRecordGhostPath(string levelId, int playerCount)
    {
        LevelSource source = LevelIdentity.GetSource(levelId);
        int clamped = SteamLeaderboardService.ClampPlayerCount(playerCount);
        return Path.Combine(
            UserDataPaths.GetGhostsRoot(source),
            $"{levelId.Replace(':', '_')}_WorldRecord_p{clamped}.replay");
    }

    private static string GetMetaPath(string levelId, int playerCount) =>
        GetWorldRecordGhostPath(levelId, playerCount) + ".meta.json";

    /// <summary>
    /// Prefer ReplayId from score details (updated with KeepBest uploads) over the
    /// attached GhostId, which can lag or stay stuck on an older AttachLeaderboardUGC.
    /// </summary>
    public static ulong ResolveGhostUgcHandle(SteamLeaderboardEntry entry)
    {
        if (entry.ReplayId != 0)
        {
            return entry.ReplayId;
        }

        return entry.GhostId;
    }

    /// <summary>
    /// Makes sure the newest World Record ghost for this player-count board is
    /// cached locally. Runs fully in the background (Steam async calls); the
    /// callback reports whether a valid cached ghost exists afterwards.
    /// </summary>
    public void EnsureWorldRecordGhost(string levelId, int playerCount, Action<bool>? onReady = null)
    {
        int clamped = SteamLeaderboardService.ClampPlayerCount(playerCount);
        if (!IsAvailable || !SupportsWorldRecordGhost(levelId))
        {
            onReady?.Invoke(HasCachedWorldRecordGhost(levelId, clamped));
            return;
        }

        int levelVersion = LevelLibrary.GetLevel(levelId)?.Version ?? 1;
        _leaderboards.DownloadEntries(levelId, levelVersion, clamped, LeaderboardScope.GlobalTop, 1, entries =>
        {
            if (entries is null || entries.Count == 0)
            {
                onReady?.Invoke(HasCachedWorldRecordGhost(levelId, clamped));
                return;
            }

            SteamLeaderboardEntry worldRecord = entries[0];
            ulong ghostHandle = ResolveGhostUgcHandle(worldRecord);
            int scoreCentiseconds = (int)MathF.Round(
                BestTimeStorage.RoundToCentiseconds(worldRecord.TimeSeconds) * 100f);
            if (ghostHandle == 0)
            {
                onReady?.Invoke(HasCachedWorldRecordGhost(levelId, clamped));
                return;
            }

            WorldRecordGhostMeta? cachedMeta = TryReadMeta(levelId, clamped);
            if (cachedMeta is not null
                && cachedMeta.UgcHandle == ghostHandle
                && cachedMeta.LevelVersion == levelVersion
                && cachedMeta.PlayerCount == clamped
                && cachedMeta.ScoreCentiseconds == scoreCentiseconds
                && HasCachedWorldRecordGhost(levelId, clamped))
            {
                onReady?.Invoke(true);
                return;
            }

            string ghostPath = GetWorldRecordGhostPath(levelId, clamped);
            _replays.DownloadReplay(ghostHandle, ghostPath, success =>
            {
                if (success)
                {
                    WriteMeta(levelId, clamped, new WorldRecordGhostMeta
                    {
                        UgcHandle = ghostHandle,
                        LevelVersion = levelVersion,
                        PlayerCount = clamped,
                        ScoreCentiseconds = scoreCentiseconds
                    });
                    DiagnosticsLog.Info(
                        "SteamGhost",
                        $"World record ghost cached level={levelId} players={clamped} handle={ghostHandle} score={scoreCentiseconds}cs");
                }

                onReady?.Invoke(success && HasCachedWorldRecordGhost(levelId, clamped));
            });
        });
    }

    /// <summary>Validates through the existing replay loader without deleting on hash mismatch.</summary>
    public bool HasCachedWorldRecordGhost(string levelId, int playerCount)
    {
        string path = GetWorldRecordGhostPath(levelId, playerCount);
        if (!File.Exists(path))
        {
            return false;
        }

        return ReplayFileSerializer.TryLoad(path, invalidateOnHashMismatch: false) is not null;
    }

    public bool TryLoadWorldRecordGhost(string levelId, int playerCount, out ReplayFile replayFile)
    {
        replayFile = null!;
        ReplayFile? loaded = ReplayFileSerializer.TryLoad(
            GetWorldRecordGhostPath(levelId, playerCount),
            invalidateOnHashMismatch: false);
        if (loaded is null)
        {
            return false;
        }

        replayFile = loaded;
        return true;
    }

    /// <summary>Drops cached WR ghosts for every player-count board of a level.</summary>
    public static void InvalidateWorldRecordGhost(string levelId)
    {
        for (int playerCount = 1; playerCount <= MaxPlayerCounts; playerCount++)
        {
            ReplayFileSerializer.TryDelete(GetWorldRecordGhostPath(levelId, playerCount));
            TryDeleteMeta(levelId, playerCount);
        }

        // Legacy unscoped cache from before per-player-count boards.
        LevelSource source = LevelIdentity.GetSource(levelId);
        string legacyPath = Path.Combine(
            UserDataPaths.GetGhostsRoot(source),
            $"{levelId.Replace(':', '_')}_WorldRecord.replay");
        ReplayFileSerializer.TryDelete(legacyPath);
        try
        {
            string legacyMeta = legacyPath + ".meta.json";
            if (File.Exists(legacyMeta))
            {
                File.Delete(legacyMeta);
            }
        }
        catch
        {
        }
    }

    /// <summary>Drops one player-count WR cache (used after a local record upload).</summary>
    public static void InvalidateWorldRecordGhost(string levelId, int playerCount)
    {
        int clamped = SteamLeaderboardService.ClampPlayerCount(playerCount);
        ReplayFileSerializer.TryDelete(GetWorldRecordGhostPath(levelId, clamped));
        TryDeleteMeta(levelId, clamped);
    }

    private static WorldRecordGhostMeta? TryReadMeta(string levelId, int playerCount)
    {
        try
        {
            string path = GetMetaPath(levelId, playerCount);
            if (!File.Exists(path))
            {
                return null;
            }

            return JsonSerializer.Deserialize<WorldRecordGhostMeta>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    private static void WriteMeta(string levelId, int playerCount, WorldRecordGhostMeta meta)
    {
        try
        {
            File.WriteAllText(GetMetaPath(levelId, playerCount), JsonSerializer.Serialize(meta));
        }
        catch
        {
        }
    }

    private static void TryDeleteMeta(string levelId, int playerCount)
    {
        try
        {
            string path = GetMetaPath(levelId, playerCount);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}
