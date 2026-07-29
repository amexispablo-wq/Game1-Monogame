#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using Steamworks;

namespace ColorBlocks;

public enum LeaderboardScope
{
    GlobalTop,
    AroundUser,
    Friends
}

/// <summary>What a completed run uploads to Steam. One entry per run, even for parties.</summary>
public sealed class SteamLeaderboardRecord
{
    public string LevelId { get; init; } = string.Empty;
    public int LevelVersion { get; init; } = 1;
    public float TimeSeconds { get; init; }
    public int PlayerCount { get; init; } = 1;
    public IReadOnlyList<ulong> SteamIds { get; init; } = Array.Empty<ulong>();
    /// <summary>UGC handle of the shared best replay (also serves as GhostId). 0 = none.</summary>
    public ulong ReplayUgcHandle { get; init; }
}

/// <summary>A decoded leaderboard row ready for UI.</summary>
public sealed class SteamLeaderboardEntry
{
    public int Rank { get; init; }
    public float TimeSeconds { get; init; }
    public ulong OwnerSteamId { get; init; }
    public IReadOnlyList<ulong> SteamIds { get; init; } = Array.Empty<ulong>();
    public IReadOnlyList<string> PlayerNames { get; init; } = Array.Empty<string>();
    public int PlayerCount { get; init; } = 1;
    public DateTime CompletionDateUtc { get; init; }
    public string GameVersion { get; init; } = string.Empty;
    public uint BuildGuidPrefix { get; init; }
    public int LevelVersion { get; init; } = 1;
    /// <summary>UGC handle of the shared replay stored in details. 0 = none.</summary>
    public ulong ReplayId { get; init; }
    /// <summary>UGC handle attached to the entry (AttachLeaderboardUGC). 0 = none.</summary>
    public ulong GhostId { get; init; }
    public bool IsLocalUser { get; init; }
    public bool IsFriend { get; init; }
    /// <summary>Client-side flag for empty details / absurd times — UI warning only.</summary>
    public bool IsSuspicious { get; init; }
}

/// <summary>
/// Steam Leaderboards for Official and Workshop levels. Local levels are never uploaded.
/// Versioning reuses the existing level Version field. Boards are also split by player
/// count so a 2-player run only competes on the 2-player board:
/// "{levelId}_v{version}_p{playerCount}_f4" (seconds×10000).
/// Same-version legacy boards without _f4 (centiseconds) are merged on download for display —
/// old scores pad to four fractional digits (÷100 → time with trailing 00). Uploads only go to _f4.
/// </summary>
public sealed class SteamLeaderboardService
{
    private const int DetailsSchemaVersion = 1;
    private const int MaxDetailInts = 18;
    private const int MaxTrackedPlayers = 4;

    private readonly SteamManager _steam;
    private readonly Dictionary<string, SteamLeaderboard_t> _boardHandles = new(StringComparer.Ordinal);

    public SteamLeaderboardService(SteamManager steam)
    {
        _steam = steam;
    }

    public bool IsAvailable => _steam.IsInitialized;

    /// <summary>Only Official and Workshop levels have Steam leaderboards.</summary>
    public static bool SupportsLeaderboards(string levelId) =>
        LevelIdentity.GetSource(levelId) != LevelSource.Local;

    public static int ClampPlayerCount(int playerCount) =>
        Math.Clamp(playerCount, 1, MaxTrackedPlayers);

    /// <summary>Current board (four fractional digits / seconds×10000).</summary>
    public static string GetLeaderboardName(string levelId, int levelVersion, int playerCount) =>
        $"{levelId.Replace(':', '_')}_v{Math.Max(1, levelVersion)}_p{ClampPlayerCount(playerCount)}_f4";

    /// <summary>Pre-_f4 board for the same level version (centiseconds). Read-only merge source.</summary>
    public static string GetLegacyLeaderboardName(string levelId, int levelVersion, int playerCount) =>
        $"{levelId.Replace(':', '_')}_v{Math.Max(1, levelVersion)}_p{ClampPlayerCount(playerCount)}";

    public void UploadRecord(SteamLeaderboardRecord record, Action<bool>? onComplete = null)
    {
        if (!IsAvailable || !SupportsLeaderboards(record.LevelId))
        {
            onComplete?.Invoke(false);
            return;
        }

        int playerCount = ClampPlayerCount(record.PlayerCount);
        string boardName = GetLeaderboardName(record.LevelId, record.LevelVersion, playerCount);
        FindBoard(boardName, createIfMissing: true, board =>
        {
            if (board.m_SteamLeaderboard == 0)
            {
                onComplete?.Invoke(false);
                return;
            }

            int score = BestTimeStorage.ToLeaderboardScore(record.TimeSeconds);
            int[] details = EncodeDetails(record, playerCount);

            SteamCallTracker.Track<LeaderboardScoreUploaded_t>(
                SteamUserStats.UploadLeaderboardScore(
                    board,
                    ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodKeepBest,
                    score,
                    details,
                    details.Length),
                (uploaded, ioFailure) =>
                {
                    bool success = !ioFailure && uploaded.m_bSuccess == 1;
                    DiagnosticsLog.Info(
                        "SteamLeaderboard",
                        $"Upload board='{boardName}' score={score} (x10000) changed={(success ? uploaded.m_bScoreChanged : 0)} ok={success}");

                    // Always re-attach UGC when we have a fresh share handle and the score was
                    // accepted as a new best. Waiting for Attach before onComplete ensures
                    // subsequent WR ghost downloads see the updated attachment when present.
                    if (success && uploaded.m_bScoreChanged == 1 && record.ReplayUgcHandle != 0)
                    {
                        SteamCallTracker.Track<LeaderboardUGCSet_t>(
                            SteamUserStats.AttachLeaderboardUGC(board, new UGCHandle_t(record.ReplayUgcHandle)),
                            (attached, attachIoFailure) =>
                            {
                                bool attachedOk = !attachIoFailure && attached.m_eResult == EResult.k_EResultOK;
                                DiagnosticsLog.Info(
                                    "SteamLeaderboard",
                                    $"AttachUGC board='{boardName}' handle={record.ReplayUgcHandle} ok={attachedOk} result={attached.m_eResult}");
                                onComplete?.Invoke(true);
                            });
                        return;
                    }

                    onComplete?.Invoke(success);
                });
        });
    }

    /// <summary>
    /// Downloads entries for the current _f4 board and merges same-version legacy
    /// centisecond board (÷100 → time with trailing 00 in FormatTime). Prefer _f4 when the
    /// same Steam user appears on both. Callback receives null only if both fail.
    /// </summary>
    public void DownloadEntries(
        string levelId,
        int levelVersion,
        int playerCount,
        LeaderboardScope scope,
        int maxEntries,
        Action<IReadOnlyList<SteamLeaderboardEntry>?> onComplete)
    {
        if (!IsAvailable || !SupportsLeaderboards(levelId))
        {
            onComplete(null);
            return;
        }

        int clamped = ClampPlayerCount(playerCount);
        int version = Math.Max(1, levelVersion);
        string currentName = GetLeaderboardName(levelId, version, clamped);
        string legacyName = GetLegacyLeaderboardName(levelId, version, clamped);

        DownloadBoardEntries(currentName, createIfMissing: true, scope, maxEntries, scoreDivisor: 10000f, current =>
        {
            DownloadBoardEntries(legacyName, createIfMissing: false, scope, maxEntries, scoreDivisor: 100f, legacy =>
            {
                if (current is null && legacy is null)
                {
                    onComplete(null);
                    return;
                }

                IReadOnlyList<SteamLeaderboardEntry> merged = MergeSameVersionBoards(current, legacy, maxEntries);
                DiagnosticsLog.Info(
                    "SteamLeaderboard",
                    $"Merge v{version} p{clamped}: f4={current?.Count ?? 0} legacy={legacy?.Count ?? 0} → {merged.Count}");
                onComplete(merged);
            });
        });
    }

    private void DownloadBoardEntries(
        string boardName,
        bool createIfMissing,
        LeaderboardScope scope,
        int maxEntries,
        float scoreDivisor,
        Action<IReadOnlyList<SteamLeaderboardEntry>?> onComplete)
    {
        FindBoard(boardName, createIfMissing, board =>
        {
            if (board.m_SteamLeaderboard == 0)
            {
                // Missing legacy board is empty, not a hard failure.
                onComplete(createIfMissing ? null : Array.Empty<SteamLeaderboardEntry>());
                return;
            }

            (ELeaderboardDataRequest request, int start, int end) = scope switch
            {
                LeaderboardScope.AroundUser => (ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobalAroundUser, -maxEntries / 2, maxEntries / 2),
                LeaderboardScope.Friends => (ELeaderboardDataRequest.k_ELeaderboardDataRequestFriends, 1, maxEntries),
                _ => (ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobal, 1, maxEntries)
            };

            SteamCallTracker.Track<LeaderboardScoresDownloaded_t>(
                SteamUserStats.DownloadLeaderboardEntries(board, request, start, end),
                (downloaded, ioFailure) =>
                {
                    if (ioFailure)
                    {
                        onComplete(createIfMissing ? null : Array.Empty<SteamLeaderboardEntry>());
                        return;
                    }

                    var entries = new List<SteamLeaderboardEntry>(downloaded.m_cEntryCount);
                    var details = new int[MaxDetailInts];
                    for (int i = 0; i < downloaded.m_cEntryCount; i++)
                    {
                        if (!SteamUserStats.GetDownloadedLeaderboardEntry(
                            downloaded.m_hSteamLeaderboardEntries, i, out LeaderboardEntry_t raw, details, details.Length))
                        {
                            continue;
                        }

                        entries.Add(DecodeEntry(raw, details, scoreDivisor));
                    }

                    onComplete(entries);
                });
        });
    }

    /// <summary>
    /// Legacy first, then _f4 overwrites same OwnerSteamId. Sort by time, re-rank 1..N.
    /// Same level version only — callers must use matching board name helpers.
    /// </summary>
    private static IReadOnlyList<SteamLeaderboardEntry> MergeSameVersionBoards(
        IReadOnlyList<SteamLeaderboardEntry>? currentF4,
        IReadOnlyList<SteamLeaderboardEntry>? legacyCs,
        int maxEntries)
    {
        var byOwner = new Dictionary<ulong, SteamLeaderboardEntry>();

        if (legacyCs is not null)
        {
            foreach (SteamLeaderboardEntry entry in legacyCs)
            {
                byOwner[entry.OwnerSteamId] = entry;
            }
        }

        if (currentF4 is not null)
        {
            foreach (SteamLeaderboardEntry entry in currentF4)
            {
                byOwner[entry.OwnerSteamId] = entry;
            }
        }

        var sorted = new List<SteamLeaderboardEntry>(byOwner.Values);
        sorted.Sort((a, b) => a.TimeSeconds.CompareTo(b.TimeSeconds));

        int take = Math.Min(maxEntries, sorted.Count);
        var ranked = new List<SteamLeaderboardEntry>(take);
        for (int i = 0; i < take; i++)
        {
            ranked.Add(WithRank(sorted[i], i + 1));
        }

        return ranked;
    }

    private static SteamLeaderboardEntry WithRank(SteamLeaderboardEntry entry, int rank) =>
        new()
        {
            Rank = rank,
            TimeSeconds = entry.TimeSeconds,
            OwnerSteamId = entry.OwnerSteamId,
            SteamIds = entry.SteamIds,
            PlayerNames = entry.PlayerNames,
            PlayerCount = entry.PlayerCount,
            CompletionDateUtc = entry.CompletionDateUtc,
            GameVersion = entry.GameVersion,
            BuildGuidPrefix = entry.BuildGuidPrefix,
            LevelVersion = entry.LevelVersion,
            ReplayId = entry.ReplayId,
            GhostId = entry.GhostId,
            IsLocalUser = entry.IsLocalUser,
            IsFriend = entry.IsFriend,
            IsSuspicious = entry.IsSuspicious
        };

    private void FindBoard(string boardName, bool createIfMissing, Action<SteamLeaderboard_t> onFound)
    {
        if (_boardHandles.TryGetValue(boardName, out SteamLeaderboard_t cached))
        {
            onFound(cached);
            return;
        }

        SteamAPICall_t call = createIfMissing
            ? SteamUserStats.FindOrCreateLeaderboard(
                boardName,
                ELeaderboardSortMethod.k_ELeaderboardSortMethodAscending,
                ELeaderboardDisplayType.k_ELeaderboardDisplayTypeTimeMilliSeconds)
            : SteamUserStats.FindLeaderboard(boardName);

        SteamCallTracker.Track<LeaderboardFindResult_t>(call, (result, ioFailure) =>
        {
            if (ioFailure || result.m_bLeaderboardFound == 0)
            {
                onFound(default);
                return;
            }

            _boardHandles[boardName] = result.m_hSteamLeaderboard;
            onFound(result.m_hSteamLeaderboard);
        });
    }

    private static int[] EncodeDetails(SteamLeaderboardRecord record, int playerCount)
    {
        var details = new int[MaxDetailInts];
        long unixNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        details[0] = DetailsSchemaVersion;
        details[1] = record.LevelVersion;
        details[2] = ClampPlayerCount(playerCount);
        details[3] = unchecked((int)(unixNow & 0xFFFFFFFF));
        details[4] = unchecked((int)(unixNow >> 32));
        details[5] = EncodeGameVersion(BuildInfo.Current.GameVersion);
        details[6] = unchecked((int)ParseBuildGuidPrefix(BuildInfo.Current.BuildGuid));

        int count = Math.Min(record.SteamIds.Count, MaxTrackedPlayers);
        details[7] = count;
        for (int i = 0; i < count; i++)
        {
            details[8 + i * 2] = unchecked((int)(record.SteamIds[i] & 0xFFFFFFFF));
            details[9 + i * 2] = unchecked((int)(record.SteamIds[i] >> 32));
        }

        details[16] = unchecked((int)(record.ReplayUgcHandle & 0xFFFFFFFF));
        details[17] = unchecked((int)(record.ReplayUgcHandle >> 32));
        return details;
    }

    private static SteamLeaderboardEntry DecodeEntry(LeaderboardEntry_t raw, int[] details, float scoreDivisor)
    {
        ulong ownerId = raw.m_steamIDUser.m_SteamID;
        ulong localId = SteamUser.GetSteamID().m_SteamID;

        int levelVersion = 1;
        int playerCount = 1;
        DateTime completionDate = DateTime.MinValue;
        string gameVersion = string.Empty;
        uint buildGuidPrefix = 0;
        ulong replayId = 0;
        var steamIds = new List<ulong>();

        if (raw.m_cDetails >= MaxDetailInts && details[0] == DetailsSchemaVersion)
        {
            levelVersion = Math.Max(1, details[1]);
            playerCount = Math.Max(1, details[2]);
            long unix = (uint)details[3] | ((long)details[4] << 32);
            if (unix > 0)
            {
                completionDate = DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
            }

            gameVersion = DecodeGameVersion(details[5]);
            buildGuidPrefix = unchecked((uint)details[6]);

            int count = Math.Clamp(details[7], 0, MaxTrackedPlayers);
            for (int i = 0; i < count; i++)
            {
                ulong id = (uint)details[8 + i * 2] | ((ulong)(uint)details[9 + i * 2] << 32);
                if (id != 0)
                {
                    steamIds.Add(id);
                }
            }

            replayId = (uint)details[16] | ((ulong)(uint)details[17] << 32);
        }

        if (steamIds.Count == 0)
        {
            steamIds.Add(ownerId);
        }

        var names = new List<string>(steamIds.Count);
        bool anyFriend = false;
        bool anyLocal = false;
        foreach (ulong id in steamIds)
        {
            var steamId = new CSteamID(id);
            SteamFriends.RequestUserInformation(steamId, bRequireNameOnly: true);
            string name = SteamFriends.GetFriendPersonaName(steamId);
            names.Add(string.IsNullOrWhiteSpace(name) || name == "[unknown]" ? $"Player {id % 10000}" : name);
            anyLocal |= id == localId;
            anyFriend |= SteamFriends.GetFriendRelationship(steamId) == EFriendRelationship.k_EFriendRelationshipFriend;
        }

        float timeSeconds = scoreDivisor > 0f
            ? Math.Max(0, raw.m_nScore) / scoreDivisor
            : BestTimeStorage.FromLeaderboardScore(raw.m_nScore);

        var entry = new SteamLeaderboardEntry
        {
            Rank = raw.m_nGlobalRank,
            TimeSeconds = timeSeconds,
            OwnerSteamId = ownerId,
            SteamIds = steamIds,
            PlayerNames = names,
            PlayerCount = playerCount,
            CompletionDateUtc = completionDate,
            GameVersion = gameVersion,
            BuildGuidPrefix = buildGuidPrefix,
            LevelVersion = levelVersion,
            ReplayId = replayId,
            GhostId = raw.m_hUGC.m_UGCHandle == UGCHandle_t.Invalid.m_UGCHandle ? 0 : raw.m_hUGC.m_UGCHandle,
            IsLocalUser = anyLocal,
            IsFriend = anyFriend && !anyLocal
        };

        return new SteamLeaderboardEntry
        {
            Rank = entry.Rank,
            TimeSeconds = entry.TimeSeconds,
            OwnerSteamId = entry.OwnerSteamId,
            SteamIds = entry.SteamIds,
            PlayerNames = entry.PlayerNames,
            PlayerCount = entry.PlayerCount,
            CompletionDateUtc = entry.CompletionDateUtc,
            GameVersion = entry.GameVersion,
            BuildGuidPrefix = entry.BuildGuidPrefix,
            LevelVersion = entry.LevelVersion,
            ReplayId = entry.ReplayId,
            GhostId = entry.GhostId,
            IsLocalUser = entry.IsLocalUser,
            IsFriend = entry.IsFriend,
            IsSuspicious = LeaderboardSanity.IsSuspiciousEntry(entry)
        };
    }

    private static int EncodeGameVersion(string version)
    {
        string[] parts = version.Split('.');
        int major = parts.Length > 0 && int.TryParse(parts[0], out int p0) ? p0 : 0;
        int minor = parts.Length > 1 && int.TryParse(parts[1], out int p1) ? p1 : 0;
        int patch = parts.Length > 2 && int.TryParse(parts[2], out int p2) ? p2 : 0;
        return major * 1_000_000 + minor * 1_000 + patch;
    }

    private static string DecodeGameVersion(int encoded)
    {
        if (encoded <= 0)
        {
            return string.Empty;
        }

        return $"{encoded / 1_000_000}.{encoded / 1_000 % 1_000}.{encoded % 1_000}";
    }

    private static uint ParseBuildGuidPrefix(string buildGuid)
    {
        if (buildGuid.Length >= 8
            && uint.TryParse(buildGuid.AsSpan(0, 8), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint prefix))
        {
            return prefix;
        }

        return 0;
    }
}
