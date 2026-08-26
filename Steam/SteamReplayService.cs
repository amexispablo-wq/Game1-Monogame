#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Steamworks;

namespace ColorBlocks;

/// <summary>
/// Uploads/downloads replay files (the existing JSON .replay format) through
/// Steam Remote Storage UGC. A shared replay is identified by its UGC handle,
/// which doubles as ReplayId/GhostId on leaderboard entries.
/// Gameplay never calls this directly; GameScene record uploads and
/// SteamGhostService downloads go through here.
/// </summary>
public sealed class SteamReplayService
{
    /// <summary>Steam FileWrite is unreliable above ~100KB on some clients; stream in chunks.</summary>
    private const int WriteChunkBytes = 100 * 1024;
    private const string RemoteReplayPrefix = "replay_";

    private readonly SteamManager _steam;

    public SteamReplayService(SteamManager steam)
    {
        _steam = steam;
    }

    public bool IsAvailable => _steam.IsInitialized;

    /// <summary>Writes a local replay file to Steam Cloud and shares it. Callback receives the UGC handle (0 on failure).</summary>
    public void ShareReplayFile(string localPath, string remoteFileName, Action<ulong> onComplete)
    {
        if (!IsAvailable)
        {
            onComplete(0);
            return;
        }

        ThreadPool.QueueUserWorkItem(_ =>
        {
            byte[] bytes;
            try
            {
                if (!File.Exists(localPath))
                {
                    MainThreadActions.Post(() => onComplete(0));
                    return;
                }

                bytes = File.ReadAllBytes(localPath);
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Info("SteamReplay", $"Read failed for '{localPath}': {ex.Message}");
                MainThreadActions.Post(() => onComplete(0));
                return;
            }

            MainThreadActions.Post(() => ShareReplayBytes(remoteFileName, bytes, onComplete));
        });
    }

    private void ShareReplayBytes(string remoteFileName, byte[] bytes, Action<ulong> onComplete)
    {
        if (!IsAvailable)
        {
            onComplete(0);
            return;
        }

        if (!SteamRemoteStorage.IsCloudEnabledForApp())
        {
            DiagnosticsLog.Info(
                "SteamReplay",
                "FileWrite blocked — Steam Cloud disabled for this App ID (enable in Partner → Steam Cloud).");
            onComplete(0);
            return;
        }

        if (!SteamRemoteStorage.IsCloudEnabledForAccount())
        {
            DiagnosticsLog.Info(
                "SteamReplay",
                "FileWrite blocked — Steam Cloud disabled for this account (Steam → Settings → Cloud).");
            onComplete(0);
            return;
        }

        // Unique remote names avoid stale UGC handles, but fill Cloud quota if never pruned.
        PruneOldRemoteReplays(remoteFileName, bytes.Length);

        if (!TryWriteRemoteFile(remoteFileName, bytes))
        {
            // One more prune pass then retry — quota may have been full of orphans.
            PruneAllRemoteReplays(keepRemoteFileName: null);
            if (!TryWriteRemoteFile(remoteFileName, bytes))
            {
                LogCloudQuota("FileWrite failed after prune");
                onComplete(0);
                return;
            }
        }

        SteamCallTracker.Track<RemoteStorageFileShareResult_t>(
            SteamRemoteStorage.FileShare(remoteFileName),
            (result, ioFailure) =>
            {
                if (ioFailure || result.m_eResult != EResult.k_EResultOK)
                {
                    DiagnosticsLog.Info(
                        "SteamReplay",
                        $"FileShare failed for '{remoteFileName}' result={result.m_eResult}.");
                    onComplete(0);
                    return;
                }

                ulong handle = result.m_hFile.m_UGCHandle;
                DiagnosticsLog.Info(
                    "SteamReplay",
                    $"FileShare ok '{remoteFileName}' handle={handle} bytes={bytes.Length}");

                // Keep the just-shared file; drop older unique replay_* names for quota.
                PruneOldRemoteReplays(remoteFileName, keepBytesBudget: 0);
                onComplete(handle);
            });
    }

    /// <summary>Downloads a shared replay/ghost by UGC handle into a local file.</summary>
    public void DownloadReplay(ulong ugcHandle, string destinationPath, Action<bool> onComplete)
    {
        if (!IsAvailable || ugcHandle == 0)
        {
            onComplete(false);
            return;
        }

        var handle = new UGCHandle_t(ugcHandle);
        SteamCallTracker.Track<RemoteStorageDownloadUGCResult_t>(
            SteamRemoteStorage.UGCDownload(handle, 0),
            (result, ioFailure) =>
            {
                if (ioFailure || result.m_eResult != EResult.k_EResultOK || result.m_nSizeInBytes <= 0)
                {
                    DiagnosticsLog.Info(
                        "SteamReplay",
                        $"UGCDownload failed handle={ugcHandle} result={result.m_eResult}.");
                    onComplete(false);
                    return;
                }

                var buffer = new byte[result.m_nSizeInBytes];
                int read = SteamRemoteStorage.UGCRead(
                    handle, buffer, buffer.Length, 0, EUGCReadAction.k_EUGCRead_Close);
                if (read != buffer.Length)
                {
                    DiagnosticsLog.Info(
                        "SteamReplay",
                        $"UGCRead incomplete handle={ugcHandle} read={read}/{buffer.Length}.");
                    onComplete(false);
                    return;
                }

                try
                {
                    string? directory = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.WriteAllBytes(destinationPath, buffer);
                    onComplete(true);
                }
                catch (Exception ex)
                {
                    DiagnosticsLog.Info("SteamReplay", $"Write failed for '{destinationPath}': {ex.Message}");
                    onComplete(false);
                }
            });
    }

    /// <summary>
    /// Unique Steam Cloud name per upload so FileShare returns a fresh UGC handle.
    /// Reusing a fixed name can keep the same handle and stale CDN/local ghost caches.
    /// Old unique names are pruned after share so Cloud quota does not fill forever.
    /// </summary>
    public static string GetRemoteReplayName(string levelId, int playerCount, int scoreCentiseconds) =>
        $"{RemoteReplayPrefix}{levelId.Replace(':', '_')}_p{SteamLeaderboardService.ClampPlayerCount(playerCount)}_{scoreCentiseconds}_{DateTime.UtcNow.Ticks}.replay";

    private static bool TryWriteRemoteFile(string remoteFileName, byte[] bytes)
    {
        // Prefer stream API — FileWrite often fails for mid-size replays (~200KB+).
        if (TryWriteRemoteFileStream(remoteFileName, bytes))
        {
            return true;
        }

        if (SteamRemoteStorage.FileWrite(remoteFileName, bytes, bytes.Length))
        {
            return true;
        }

        DiagnosticsLog.Info(
            "SteamReplay",
            $"FileWrite failed for '{remoteFileName}' ({bytes.Length} bytes).");
        return false;
    }

    private static bool TryWriteRemoteFileStream(string remoteFileName, byte[] bytes)
    {
        UGCFileWriteStreamHandle_t stream = SteamRemoteStorage.FileWriteStreamOpen(remoteFileName);
        if (stream == UGCFileWriteStreamHandle_t.Invalid)
        {
            DiagnosticsLog.Info("SteamReplay", $"FileWriteStreamOpen failed for '{remoteFileName}'.");
            return false;
        }

        try
        {
            for (int offset = 0; offset < bytes.Length; offset += WriteChunkBytes)
            {
                int chunk = Math.Min(WriteChunkBytes, bytes.Length - offset);
                var slice = new byte[chunk];
                Buffer.BlockCopy(bytes, offset, slice, 0, chunk);
                if (!SteamRemoteStorage.FileWriteStreamWriteChunk(stream, slice, chunk))
                {
                    DiagnosticsLog.Info(
                        "SteamReplay",
                        $"FileWriteStreamWriteChunk failed '{remoteFileName}' offset={offset}/{bytes.Length}.");
                    SteamRemoteStorage.FileWriteStreamCancel(stream);
                    return false;
                }
            }

            if (!SteamRemoteStorage.FileWriteStreamClose(stream))
            {
                DiagnosticsLog.Info("SteamReplay", $"FileWriteStreamClose failed for '{remoteFileName}'.");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Info("SteamReplay", $"FileWriteStream throw '{remoteFileName}': {ex.Message}");
            try { SteamRemoteStorage.FileWriteStreamCancel(stream); } catch { /* ignore */ }
            return false;
        }
    }

    /// <summary>
    /// Deletes older unique replay_* cloud files so quota stays available.
    /// Keeps <paramref name="keepRemoteFileName"/> when set.
    /// </summary>
    private static void PruneOldRemoteReplays(string keepRemoteFileName, int keepBytesBudget)
    {
        string keepPrefix = ExtractReplayFamilyPrefix(keepRemoteFileName);
        int fileCount = SteamRemoteStorage.GetFileCount();
        var toDelete = new List<string>();

        for (int i = 0; i < fileCount; i++)
        {
            string name = SteamRemoteStorage.GetFileNameAndSize(i, out int size);
            if (string.IsNullOrEmpty(name)
                || !name.StartsWith(RemoteReplayPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(keepRemoteFileName)
                && string.Equals(name, keepRemoteFileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Prefer deleting same level/player family first; otherwise all replay_* orphans.
            if (string.IsNullOrEmpty(keepPrefix)
                || name.StartsWith(keepPrefix, StringComparison.OrdinalIgnoreCase)
                || keepBytesBudget <= 0)
            {
                toDelete.Add(name);
            }
        }

        int deleted = 0;
        foreach (string name in toDelete)
        {
            if (SteamRemoteStorage.FileDelete(name))
            {
                deleted++;
            }
        }

        if (deleted > 0)
        {
            DiagnosticsLog.Info("SteamReplay", $"Pruned {deleted} old remote replay file(s).");
        }
    }

    private static void PruneAllRemoteReplays(string? keepRemoteFileName)
    {
        PruneOldRemoteReplays(keepRemoteFileName ?? string.Empty, keepBytesBudget: 0);
    }

    /// <summary>
    /// Family prefix for pruning: "replay_official_Level_4_p1_" from a full unique name.
    /// </summary>
    private static string ExtractReplayFamilyPrefix(string remoteFileName)
    {
        if (string.IsNullOrEmpty(remoteFileName)
            || !remoteFileName.StartsWith(RemoteReplayPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        // replay_{level}_p{n}_{score}_{ticks}.replay → keep through p{n}_
        int pIndex = remoteFileName.IndexOf("_p", StringComparison.OrdinalIgnoreCase);
        if (pIndex < 0)
        {
            return RemoteReplayPrefix;
        }

        int afterPlayers = remoteFileName.IndexOf('_', pIndex + 2);
        if (afterPlayers < 0)
        {
            return remoteFileName;
        }

        return remoteFileName[..(afterPlayers + 1)];
    }

    private static void LogCloudQuota(string reason)
    {
        SteamRemoteStorage.GetQuota(out ulong totalBytes, out ulong availableBytes);
        var sb = new StringBuilder();
        sb.Append(reason);
        sb.Append(" quotaTotal=").Append(totalBytes);
        sb.Append(" quotaAvail=").Append(availableBytes);
        sb.Append(" cloudApp=").Append(SteamRemoteStorage.IsCloudEnabledForApp());
        sb.Append(" cloudAcct=").Append(SteamRemoteStorage.IsCloudEnabledForAccount());
        sb.Append(" files=").Append(SteamRemoteStorage.GetFileCount());
        DiagnosticsLog.Info("SteamReplay", sb.ToString());
    }
}
