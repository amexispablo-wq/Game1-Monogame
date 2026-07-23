#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ColorBlocks.Replay;
using Steamworks;

namespace ColorBlocks;

public sealed class WorkshopPublishResult
{
    public bool Success { get; init; }
    public ulong WorkshopId { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool NeedsLegalAgreement { get; init; }
}

/// <summary>Cached community metadata for a workshop item (votes, subscribers, dates).</summary>
public sealed class WorkshopItemDetails
{
    public ulong WorkshopId { get; init; }
    public string Title { get; init; } = string.Empty;
    public ulong OwnerSteamId { get; init; }
    public uint VotesUp { get; init; }
    public uint VotesDown { get; init; }
    public ulong Subscribers { get; init; }
    public DateTime PublishedDateUtc { get; init; }
    public DateTime UpdatedDateUtc { get; init; }
    public ERemoteStoragePublishedFileVisibility Visibility { get; init; }

    public string VisibilityLabel => Visibility switch
    {
        ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic => "Public",
        ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityFriendsOnly => "Friends Only",
        ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPrivate => "Private",
        ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityUnlisted => "Unlisted",
        _ => "Unknown"
    };
}

/// <summary>
/// Steam Workshop (UGC) integration for user-created levels only.
/// Upload path: Local level -> CreateItem/SubmitItemUpdate (Official levels are rejected).
/// Download path: subscriptions sync into the existing WorkshopLevels layout
/// (%LocalAppData%/Color Blocks/Workshop/{id}/level.json), which LevelLibrary
/// already lists as read-only Workshop levels. Editing goes through the existing
/// Create Local Copy flow (LevelLibrary.DuplicateLevel), exactly like Portal 2.
/// </summary>
public sealed class SteamWorkshopService : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly SteamManager _steam;
    private readonly Dictionary<ulong, WorkshopItemDetails> _detailsCache = new();
    private readonly HashSet<ulong> _pendingDetailQueries = new();
    private Callback<ItemInstalled_t>? _itemInstalled;
    private Callback<DownloadItemResult_t>? _downloadResult;
    private bool _isDisposed;

    public SteamWorkshopService(SteamManager steam)
    {
        _steam = steam;
    }

    public bool IsAvailable => _steam.IsInitialized;
    public bool IsPublishing { get; private set; }

    /// <summary>Bumped whenever the local workshop level folder changes; UI polls this to refresh lists.</summary>
    public int ChangeStamp { get; private set; }

    public void Initialize()
    {
        if (!IsAvailable || _itemInstalled is not null)
        {
            return;
        }

        _itemInstalled = Callback<ItemInstalled_t>.Create(OnItemInstalled);
        _downloadResult = Callback<DownloadItemResult_t>.Create(OnDownloadItemResult);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _itemInstalled?.Dispose();
        _downloadResult?.Dispose();
    }

    // ------------------------------------------------------------------
    // Publish / update (Local levels only — Official can never be uploaded)
    // ------------------------------------------------------------------

    public void PublishLevel(string levelId, Action<WorkshopPublishResult> onComplete)
    {
        if (!IsAvailable)
        {
            onComplete(Fail("Steam is not available."));
            return;
        }

        if (IsPublishing)
        {
            onComplete(Fail("Another upload is already in progress."));
            return;
        }

        LevelMetadata? metadata = LevelLibrary.GetLevel(levelId);
        if (metadata is null || metadata.Source != LevelSource.Local)
        {
            onComplete(Fail("Only local levels can be uploaded to the Workshop."));
            return;
        }

        IsPublishing = true;
        void Complete(WorkshopPublishResult result)
        {
            IsPublishing = false;
            onComplete(result);
        }

        if (ulong.TryParse(metadata.WorkshopId, out ulong existingId) && existingId != 0)
        {
            SubmitUpdate(metadata, new PublishedFileId_t(existingId), isNewItem: false, Complete);
            return;
        }

        SteamCallTracker.Track<CreateItemResult_t>(
            SteamUGC.CreateItem(SteamUtils.GetAppID(), EWorkshopFileType.k_EWorkshopFileTypeCommunity),
            (created, ioFailure) =>
            {
                if (ioFailure || created.m_eResult != EResult.k_EResultOK)
                {
                    Complete(Fail(FormatCreateFailure(created.m_eResult, ioFailure)));
                    return;
                }

                WriteWorkshopFieldsToLocalLevel(metadata, created.m_nPublishedFileId.m_PublishedFileId);
                if (created.m_bUserNeedsToAcceptWorkshopLegalAgreement)
                {
                    OpenWorkshopPage(created.m_nPublishedFileId.m_PublishedFileId);
                }

                SubmitUpdate(metadata, created.m_nPublishedFileId, isNewItem: true, Complete);
            });
    }

    private void SubmitUpdate(
        LevelMetadata metadata,
        PublishedFileId_t fileId,
        bool isNewItem,
        Action<WorkshopPublishResult> onComplete)
    {
        string stagingFolder;
        try
        {
            stagingFolder = BuildStagingFolder(metadata, fileId.m_PublishedFileId);
        }
        catch (Exception ex)
        {
            onComplete(Fail($"Failed to stage workshop content: {ex.Message}"));
            return;
        }

        string title = string.IsNullOrWhiteSpace(metadata.Name) ? "Untitled Level" : metadata.Name.Trim();
        string author = string.IsNullOrWhiteSpace(metadata.Author) ? "Unknown" : metadata.Author.Trim();
        string description = $"A Color Blocks level by {author}.";

        UGCUpdateHandle_t update = SteamUGC.StartItemUpdate(SteamUtils.GetAppID(), fileId);
        if (update == UGCUpdateHandle_t.Invalid)
        {
            onComplete(Fail("Workshop update handle is invalid."));
            return;
        }

        if (!SteamUGC.SetItemTitle(update, title))
        {
            DiagnosticsLog.Info("SteamWorkshop", $"SetItemTitle failed level={metadata.Id} title='{title}'");
            onComplete(Fail("Workshop SetItemTitle failed. Check the level name."));
            return;
        }

        if (!SteamUGC.SetItemDescription(update, description))
        {
            DiagnosticsLog.Info("SteamWorkshop", $"SetItemDescription failed level={metadata.Id}");
            onComplete(Fail("Workshop SetItemDescription failed."));
            return;
        }

        if (!SteamUGC.SetItemContent(update, stagingFolder))
        {
            DiagnosticsLog.Info("SteamWorkshop", $"SetItemContent failed level={metadata.Id} path='{stagingFolder}'");
            onComplete(Fail("Workshop SetItemContent failed. Content folder is invalid."));
            return;
        }

        if (isNewItem
            && !SteamUGC.SetItemVisibility(
                update,
                ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic))
        {
            DiagnosticsLog.Info("SteamWorkshop", $"SetItemVisibility failed level={metadata.Id}");
            onComplete(Fail("Workshop SetItemVisibility failed."));
            return;
        }

        string? previewPath = TryFindPreviewFile(metadata.Id);
        if (previewPath is not null)
        {
            if (!SteamUGC.SetItemPreview(update, previewPath))
            {
                DiagnosticsLog.Info(
                    "SteamWorkshop",
                    $"SetItemPreview failed level={metadata.Id} path='{previewPath}' — continuing without preview");
            }
        }

        SteamCallTracker.Track<SubmitItemUpdateResult_t>(
            SteamUGC.SubmitItemUpdate(update, $"Version {metadata.Version}"),
            (submitted, ioFailure) =>
            {
                if (ioFailure || submitted.m_eResult != EResult.k_EResultOK)
                {
                    onComplete(Fail(FormatSubmitFailure(submitted.m_eResult, ioFailure)));
                    return;
                }

                WriteWorkshopFieldsToLocalLevel(metadata, fileId.m_PublishedFileId);
                _detailsCache.Remove(fileId.m_PublishedFileId);
                DiagnosticsLog.Info("SteamWorkshop", $"Published level={metadata.Id} workshopId={fileId.m_PublishedFileId} new={isNewItem}");
                onComplete(new WorkshopPublishResult
                {
                    Success = true,
                    WorkshopId = fileId.m_PublishedFileId,
                    NeedsLegalAgreement = submitted.m_bUserNeedsToAcceptWorkshopLegalAgreement,
                    Message = isNewItem ? "Level uploaded to the Workshop." : "Workshop item updated."
                });
            });
    }

    private static string BuildStagingFolder(LevelMetadata metadata, ulong workshopId)
    {
        string staging = Path.GetFullPath(
            Path.Combine(UserDataPaths.Temporary, "WorkshopStaging", workshopId.ToString()));
        if (Directory.Exists(staging))
        {
            Directory.Delete(staging, recursive: true);
        }

        Directory.CreateDirectory(staging);
        string levelSource = Path.GetFullPath(metadata.FilePath);
        File.Copy(levelSource, Path.Combine(staging, "level.json"), overwrite: true);
        return staging;
    }

    private static string? TryFindPreviewFile(string levelId)
    {
        const long MinPreviewBytes = 16;
        try
        {
            string previewsRoot = LevelContentPaths.GetPreviewsRoot(LevelIdentity.GetSource(levelId));
            if (!Directory.Exists(previewsRoot))
            {
                return null;
            }

            string stem = levelId.Replace(':', '_');
            foreach (string file in Directory.EnumerateFiles(previewsRoot, "*.png"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (!name.EndsWith(stem, StringComparison.OrdinalIgnoreCase)
                    && !Path.GetFileName(file).Contains(levelId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string fullPath = Path.GetFullPath(file);
                var info = new FileInfo(fullPath);
                if (!info.Exists || info.Length < MinPreviewBytes)
                {
                    DiagnosticsLog.Info(
                        "SteamWorkshop",
                        $"Skipping invalid preview level={levelId} path='{fullPath}' size={info.Length}");
                    continue;
                }

                return fullPath;
            }
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Info("SteamWorkshop", $"Preview lookup failed level={levelId}: {ex.Message}");
        }

        return null;
    }

    private static string FormatCreateFailure(EResult result, bool ioFailure)
    {
        if (ioFailure)
        {
            return "Workshop item creation failed (network/IO error).";
        }

        if (result == EResult.k_EResultInvalidParam)
        {
            return "Workshop item creation failed (InvalidParam). "
                + "Enable ISteamUGC / Workshop file transfer for this App ID in Steamworks Partner, "
                + "then restart Steam and retry.";
        }

        return $"Workshop item creation failed ({result}).";
    }

    private static string FormatSubmitFailure(EResult result, bool ioFailure)
    {
        if (ioFailure)
        {
            return "Workshop upload failed (network/IO error).";
        }

        if (result == EResult.k_EResultInvalidParam)
        {
            return "Workshop upload failed (InvalidParam). "
                + "Enable ISteamUGC / Workshop file transfer for this App ID in Steamworks Partner, "
                + "then restart Steam and retry.";
        }

        return $"Workshop upload failed ({result}).";
    }

    /// <summary>Persists WorkshopId/OwnerSteamId/LastSync into the local level file without bumping its version.</summary>
    private void WriteWorkshopFieldsToLocalLevel(LevelMetadata metadata, ulong workshopId)
    {
        try
        {
            LevelData? data = JsonSerializer.Deserialize<LevelData>(File.ReadAllText(metadata.FilePath), JsonOptions);
            if (data is null)
            {
                return;
            }

            data.WorkshopId = workshopId.ToString();
            data.OwnerSteamId = SteamUser.GetSteamID().m_SteamID.ToString();
            data.LastSync = DateTime.UtcNow;
            File.WriteAllText(metadata.FilePath, JsonSerializer.Serialize(data, JsonOptions));
            metadata.WorkshopId = workshopId.ToString();
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Info("SteamWorkshop", $"Failed to persist workshop id for {metadata.Id}: {ex.Message}");
        }
    }

    // ------------------------------------------------------------------
    // Subscriptions / downloads (read-only Workshop levels)
    // ------------------------------------------------------------------

    public void Subscribe(ulong workshopId, Action<bool>? onComplete = null)
    {
        if (!IsAvailable)
        {
            onComplete?.Invoke(false);
            return;
        }

        SteamCallTracker.Track<RemoteStorageSubscribePublishedFileResult_t>(
            SteamUGC.SubscribeItem(new PublishedFileId_t(workshopId)),
            (result, ioFailure) =>
            {
                bool success = !ioFailure && result.m_eResult == EResult.k_EResultOK;
                if (success)
                {
                    SteamUGC.DownloadItem(new PublishedFileId_t(workshopId), bHighPriority: true);
                }

                onComplete?.Invoke(success);
            });
    }

    public void Unsubscribe(ulong workshopId, Action<bool>? onComplete = null)
    {
        if (!IsAvailable)
        {
            onComplete?.Invoke(false);
            return;
        }

        SteamCallTracker.Track<RemoteStorageUnsubscribePublishedFileResult_t>(
            SteamUGC.UnsubscribeItem(new PublishedFileId_t(workshopId)),
            (result, ioFailure) =>
            {
                bool success = !ioFailure && result.m_eResult == EResult.k_EResultOK;
                if (success)
                {
                    RemoveDownloadedItem(workshopId);
                }

                onComplete?.Invoke(success);
            });
    }

    /// <summary>
    /// Mirrors all Steam subscriptions into the local WorkshopLevels folder and removes
    /// items the user unsubscribed from. Downloads happen in the background via Steam;
    /// installed items are copied when ItemInstalled fires.
    /// </summary>
    public void SyncSubscribedItems()
    {
        if (!IsAvailable)
        {
            return;
        }

        uint count = SteamUGC.GetNumSubscribedItems();
        var subscribed = new PublishedFileId_t[count];
        if (count > 0)
        {
            SteamUGC.GetSubscribedItems(subscribed, count);
        }

        var subscribedSet = new HashSet<ulong>();
        foreach (PublishedFileId_t id in subscribed)
        {
            subscribedSet.Add(id.m_PublishedFileId);
            uint state = SteamUGC.GetItemState(id);
            bool installed = (state & (uint)EItemState.k_EItemStateInstalled) != 0;
            bool needsUpdate = (state & (uint)EItemState.k_EItemStateNeedsUpdate) != 0;

            if (installed && !needsUpdate)
            {
                CopyInstalledItem(id);
            }
            else
            {
                SteamUGC.DownloadItem(id, bHighPriority: false);
            }
        }

        // Drop local copies of items the user is no longer subscribed to.
        try
        {
            string workshopRoot = UserDataPaths.GetWorkshopRoot();
            if (Directory.Exists(workshopRoot))
            {
                foreach (string folder in Directory.GetDirectories(workshopRoot))
                {
                    string name = Path.GetFileName(folder);
                    if (ulong.TryParse(name, out ulong folderId) && !subscribedSet.Contains(folderId))
                    {
                        Directory.Delete(folder, recursive: true);
                        ChangeStamp++;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Info("SteamWorkshop", $"Unsubscribed cleanup failed: {ex.Message}");
        }
    }

    private void OnItemInstalled(ItemInstalled_t data)
    {
        if (data.m_unAppID != SteamUtils.GetAppID())
        {
            return;
        }

        CopyInstalledItem(data.m_nPublishedFileId);
    }

    private void OnDownloadItemResult(DownloadItemResult_t data)
    {
        if (data.m_unAppID != SteamUtils.GetAppID() || data.m_eResult != EResult.k_EResultOK)
        {
            return;
        }

        CopyInstalledItem(data.m_nPublishedFileId);
    }

    private void CopyInstalledItem(PublishedFileId_t fileId)
    {
        if (!SteamUGC.GetItemInstallInfo(fileId, out _, out string installFolder, 1024, out uint updateTimestamp))
        {
            return;
        }

        string sourceLevel = Path.Combine(installFolder, "level.json");
        if (!File.Exists(sourceLevel))
        {
            return;
        }

        ulong workshopId = fileId.m_PublishedFileId;
        string destinationLevel = UserDataPaths.GetWorkshopLevelFile(workshopId.ToString());

        try
        {
            LevelData? data = JsonSerializer.Deserialize<LevelData>(File.ReadAllText(sourceLevel), JsonOptions);
            if (data is null)
            {
                return;
            }

            string newDownloadedVersion = updateTimestamp.ToString();
            bool contentChanged = true;
            if (File.Exists(destinationLevel))
            {
                LevelData? existing = JsonSerializer.Deserialize<LevelData>(File.ReadAllText(destinationLevel), JsonOptions);
                contentChanged = existing?.DownloadedVersion != newDownloadedVersion;
                if (!contentChanged)
                {
                    return;
                }
            }

            data.WorkshopId = workshopId.ToString();
            data.DownloadedVersion = newDownloadedVersion;
            data.LastSync = DateTime.UtcNow;

            Directory.CreateDirectory(Path.GetDirectoryName(destinationLevel)!);
            File.WriteAllText(destinationLevel, JsonSerializer.Serialize(data, JsonOptions));

            string sourcePreview = Path.Combine(installFolder, "preview.png");
            if (File.Exists(sourcePreview))
            {
                File.Copy(sourcePreview, UserDataPaths.GetWorkshopPreviewFile(workshopId.ToString()), overwrite: true);
            }

            // Updated content = new level version: demote existing best to unofficial and
            // invalidate the cached best replay, reusing the existing invalidation path.
            string levelId = LevelIdentity.Compose(LevelSource.Workshop, workshopId.ToString());
            BestTimeStorage.InvalidateOfficialOnLevelEdit(levelId);

            ChangeStamp++;
            DiagnosticsLog.Info("SteamWorkshop", $"Workshop item synced id={workshopId} version={newDownloadedVersion}");
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Info("SteamWorkshop", $"Failed to copy workshop item {workshopId}: {ex.Message}");
        }
    }

    private void RemoveDownloadedItem(ulong workshopId)
    {
        try
        {
            string levelId = LevelIdentity.Compose(LevelSource.Workshop, workshopId.ToString());
            string folder = Path.GetDirectoryName(UserDataPaths.GetWorkshopLevelFile(workshopId.ToString()))!;
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
                BestTimeStorage.DeleteLevelRecord(levelId);
                ReplayStorage.DeleteBestReplay(levelId);
                SteamGhostService.InvalidateWorldRecordGhost(levelId);
                ChangeStamp++;
            }
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Info("SteamWorkshop", $"Failed to remove workshop item {workshopId}: {ex.Message}");
        }
    }

    // ------------------------------------------------------------------
    // Details (votes / subscribers) and overlay pages
    // ------------------------------------------------------------------

    /// <summary>Returns cached details and requests them in the background when missing.</summary>
    public WorkshopItemDetails? GetDetails(ulong workshopId)
    {
        if (workshopId == 0 || !IsAvailable)
        {
            return null;
        }

        if (_detailsCache.TryGetValue(workshopId, out WorkshopItemDetails? cached))
        {
            return cached;
        }

        RequestDetails(workshopId);
        return null;
    }

    private void RequestDetails(ulong workshopId)
    {
        if (!_pendingDetailQueries.Add(workshopId))
        {
            return;
        }

        var ids = new[] { new PublishedFileId_t(workshopId) };
        UGCQueryHandle_t query = SteamUGC.CreateQueryUGCDetailsRequest(ids, (uint)ids.Length);
        if (query == UGCQueryHandle_t.Invalid)
        {
            _pendingDetailQueries.Remove(workshopId);
            return;
        }

        SteamCallTracker.Track<SteamUGCQueryCompleted_t>(
            SteamUGC.SendQueryUGCRequest(query),
            (result, ioFailure) =>
            {
                _pendingDetailQueries.Remove(workshopId);
                if (!ioFailure
                    && result.m_eResult == EResult.k_EResultOK
                    && result.m_unNumResultsReturned > 0
                    && SteamUGC.GetQueryUGCResult(result.m_handle, 0, out SteamUGCDetails_t details))
                {
                    SteamUGC.GetQueryUGCStatistic(
                        result.m_handle, 0, EItemStatistic.k_EItemStatistic_NumSubscriptions, out ulong subscribers);

                    _detailsCache[workshopId] = new WorkshopItemDetails
                    {
                        WorkshopId = workshopId,
                        Title = details.m_rgchTitle,
                        OwnerSteamId = details.m_ulSteamIDOwner,
                        VotesUp = details.m_unVotesUp,
                        VotesDown = details.m_unVotesDown,
                        Subscribers = subscribers,
                        PublishedDateUtc = DateTimeOffset.FromUnixTimeSeconds(details.m_rtimeCreated).UtcDateTime,
                        UpdatedDateUtc = DateTimeOffset.FromUnixTimeSeconds(details.m_rtimeUpdated).UtcDateTime,
                        Visibility = details.m_eVisibility
                    };
                }

                SteamUGC.ReleaseQueryUGCRequest(result.m_handle);
            });
    }

    public void OpenWorkshopPage(ulong workshopId)
    {
        if (!IsAvailable || workshopId == 0)
        {
            return;
        }

        SteamFriends.ActivateGameOverlayToWebPage($"steam://url/CommunityFilePage/{workshopId}");
    }

    public void OpenWorkshopHub()
    {
        if (!IsAvailable)
        {
            return;
        }

        SteamFriends.ActivateGameOverlayToWebPage(
            $"https://steamcommunity.com/app/{SteamUtils.GetAppID().m_AppId}/workshop/");
    }

    private static WorkshopPublishResult Fail(string message) => new() { Success = false, Message = message };

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
