#nullable enable
using System;
using System.IO;
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
    private readonly SteamManager _steam;

    public SteamReplayService(SteamManager steam)
    {
        _steam = steam;
    }

    public bool IsAvailable => _steam.IsInitialized;

    /// <summary>Writes a local replay file to Steam Cloud and shares it. Callback receives the UGC handle (0 on failure).</summary>
    public void ShareReplayFile(string localPath, string remoteFileName, Action<ulong> onComplete)
    {
        if (!IsAvailable || !File.Exists(localPath))
        {
            onComplete(0);
            return;
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(localPath);
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Info("SteamReplay", $"Read failed for '{localPath}': {ex.Message}");
            onComplete(0);
            return;
        }

        if (!SteamRemoteStorage.FileWrite(remoteFileName, bytes, bytes.Length))
        {
            DiagnosticsLog.Info("SteamReplay", $"FileWrite failed for '{remoteFileName}' ({bytes.Length} bytes).");
            onComplete(0);
            return;
        }

        SteamCallTracker.Track<RemoteStorageFileShareResult_t>(
            SteamRemoteStorage.FileShare(remoteFileName),
            (result, ioFailure) =>
            {
                if (ioFailure || result.m_eResult != EResult.k_EResultOK)
                {
                    DiagnosticsLog.Info("SteamReplay", $"FileShare failed for '{remoteFileName}' result={result.m_eResult}.");
                    onComplete(0);
                    return;
                }

                onComplete(result.m_hFile.m_UGCHandle);
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
                    DiagnosticsLog.Info("SteamReplay", $"UGCDownload failed handle={ugcHandle} result={result.m_eResult}.");
                    onComplete(false);
                    return;
                }

                var buffer = new byte[result.m_nSizeInBytes];
                int read = SteamRemoteStorage.UGCRead(
                    handle, buffer, buffer.Length, 0, EUGCReadAction.k_EUGCRead_Close);
                if (read != buffer.Length)
                {
                    DiagnosticsLog.Info("SteamReplay", $"UGCRead incomplete handle={ugcHandle} read={read}/{buffer.Length}.");
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
    /// </summary>
    public static string GetRemoteReplayName(string levelId, int playerCount, int scoreCentiseconds) =>
        $"replay_{levelId.Replace(':', '_')}_p{SteamLeaderboardService.ClampPlayerCount(playerCount)}_{scoreCentiseconds}_{DateTime.UtcNow.Ticks}.replay";
}
