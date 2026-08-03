#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// <summary>True when this publish updated an existing Workshop item (not a first CreateItem).</summary>
    public bool WasUpdate { get; init; }
    public bool WasCancelled { get; init; }
    /// <summary>True when the local WorkshopId likely points at a deleted/inaccessible Steam item.</summary>
    public bool SuggestClearWorkshopId { get; init; }
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
    private bool _publishCancelRequested;

    public SteamWorkshopService(SteamManager steam)
    {
        _steam = steam;
    }

    public bool IsAvailable => _steam.IsInitialized;
    public bool IsPublishing { get; private set; }

    /// <summary>
    /// Soft-cancel the in-flight publish. Steam cannot abort SubmitItemUpdate once started;
    /// we skip post-submit success handling and bail before Submit when possible.
    /// </summary>
    public void CancelPublish()
    {
        if (!IsPublishing)
        {
            return;
        }

        _publishCancelRequested = true;
        DiagnosticsLog.Info("SteamWorkshop", "Publish cancel requested.");
    }

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

        if (!TryValidateLevelForPublish(metadata, out string validationError))
        {
            onComplete(Fail(validationError));
            return;
        }

        IsPublishing = true;
        _publishCancelRequested = false;
        void Complete(WorkshopPublishResult result)
        {
            IsPublishing = false;
            _publishCancelRequested = false;
            onComplete(result);
        }

        if (ulong.TryParse(metadata.WorkshopId, out ulong existingId) && existingId != 0)
        {
            SubmitUpdate(metadata, new PublishedFileId_t(existingId), isNewItem: false, result =>
            {
                if (!result.Success
                    && !result.WasCancelled
                    && result.SuggestClearWorkshopId)
                {
                    DiagnosticsLog.Info(
                        "SteamWorkshop",
                        $"Stale WorkshopId={existingId} for level={metadata.Id}; clearing and creating new item.");
                    ClearWorkshopFieldsFromLocalLevel(metadata);
                    BeginCreateItem(metadata, Complete);
                    return;
                }

                Complete(result);
            });
            return;
        }

        BeginCreateItem(metadata, Complete);
    }

    private void BeginCreateItem(LevelMetadata metadata, Action<WorkshopPublishResult> onComplete)
    {
        if (TryConsumeCancel(onComplete))
        {
            return;
        }

        SteamCallTracker.Track<CreateItemResult_t>(
            SteamUGC.CreateItem(SteamUtils.GetAppID(), EWorkshopFileType.k_EWorkshopFileTypeCommunity),
            (created, ioFailure) =>
            {
                if (TryConsumeCancel(onComplete))
                {
                    return;
                }

                if (ioFailure || created.m_eResult != EResult.k_EResultOK)
                {
                    onComplete(Fail(FormatCreateFailure(created.m_eResult, ioFailure)));
                    return;
                }

                WriteWorkshopFieldsToLocalLevel(metadata, created.m_nPublishedFileId.m_PublishedFileId);
                if (created.m_bUserNeedsToAcceptWorkshopLegalAgreement)
                {
                    OpenWorkshopPage(created.m_nPublishedFileId.m_PublishedFileId);
                }

                SubmitUpdate(metadata, created.m_nPublishedFileId, isNewItem: true, onComplete);
            });
    }

    private void SubmitUpdate(
        LevelMetadata metadata,
        PublishedFileId_t fileId,
        bool isNewItem,
        Action<WorkshopPublishResult> onComplete)
    {
        if (TryConsumeCancel(onComplete))
        {
            return;
        }

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
            bool previewOk = SteamUGC.SetItemPreview(update, previewPath);
            DiagnosticsLog.Info(
                "SteamWorkshop",
                $"SetItemPreview level={metadata.Id} path='{previewPath}' ok={previewOk}");
            if (!previewOk)
            {
                DiagnosticsLog.Info(
                    "SteamWorkshop",
                    $"SetItemPreview failed level={metadata.Id} — check Steam Cloud quota (previews use Cloud). Continuing without preview.");
            }
        }
        else
        {
            DiagnosticsLog.Info(
                "SteamWorkshop",
                $"Publish without preview — no PNG for level={metadata.Id}");
        }

        if (TryConsumeCancel(onComplete))
        {
            return;
        }

        SteamCallTracker.Track<SubmitItemUpdateResult_t>(
            SteamUGC.SubmitItemUpdate(update, $"Version {metadata.Version}"),
            (submitted, ioFailure) =>
            {
                // Steam cannot abort an in-flight SubmitItemUpdate. If the user cancelled,
                // still report cancelled even when Steam finished uploading.
                if (_publishCancelRequested)
                {
                    onComplete(Cancelled());
                    return;
                }

                if (ioFailure || submitted.m_eResult != EResult.k_EResultOK)
                {
                    onComplete(Fail(
                        FormatSubmitFailure(submitted.m_eResult, ioFailure),
                        suggestClearWorkshopId: !isNewItem && IsMissingOrForbiddenWorkshopResult(submitted.m_eResult)));
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
                    WasUpdate = !isNewItem,
                    Message = isNewItem ? "Level uploaded to the Workshop." : "Workshop item updated."
                });
            });
    }

    private bool TryConsumeCancel(Action<WorkshopPublishResult> onComplete)
    {
        if (!_publishCancelRequested)
        {
            return false;
        }

        onComplete(Cancelled());
        return true;
    }

    private static bool IsMissingOrForbiddenWorkshopResult(EResult result) =>
        result is EResult.k_EResultFileNotFound
            or EResult.k_EResultAccessDenied
            or EResult.k_EResultInsufficientPrivilege
            or EResult.k_EResultFail;

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
        // Whitelist: only level.json leaves staging. Preview is set separately via SetItemPreview.
        File.Copy(levelSource, Path.Combine(staging, "level.json"), overwrite: true);
        return staging;
    }

    private static bool TryValidateLevelForPublish(LevelMetadata metadata, out string error)
    {
        error = string.Empty;
        try
        {
            var info = new FileInfo(metadata.FilePath);
            if (!info.Exists)
            {
                error = "Level file is missing.";
                return false;
            }

            if (info.Length > LevelValidator.MaxLevelFileBytes)
            {
                error = $"Level file exceeds {LevelValidator.MaxLevelFileBytes / (1024 * 1024)} MB limit.";
                return false;
            }

            LevelData? data = JsonSerializer.Deserialize<LevelData>(
                File.ReadAllText(metadata.FilePath),
                JsonOptions);
            if (data is null)
            {
                error = "Level JSON could not be parsed.";
                return false;
            }

            LevelValidationResult validation = LevelValidator.Validate(data, LevelValidationProfile.Strict);
            if (!validation.IsValid)
            {
                error = $"Level failed validation: {validation.Summary}";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = $"Level validation failed: {ex.Message}";
            return false;
        }
    }

    private static readonly byte[] PngMagic = { 0x89, 0x50, 0x4E, 0x47 };
    private const long MaxPreviewBytes = 4L * 1024 * 1024;

    private static bool IsValidPngPreview(string path, long minBytes)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length < minBytes || info.Length > MaxPreviewBytes)
            {
                return false;
            }

            using FileStream stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[4];
            if (stream.Read(header) < 4)
            {
                return false;
            }

            return header.SequenceEqual(PngMagic);
        }
        catch
        {
            return false;
        }
    }

    private static readonly string[] DangerousWorkshopExtensions =
    {
        ".exe", ".dll", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".msi", ".scr", ".com"
    };

    private static bool InstallFolderHasDangerousExtras(string installFolder)
    {
        try
        {
            foreach (string file in Directory.EnumerateFiles(installFolder, "*", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                if (string.Equals(name, "level.json", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "preview.png", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string ext = Path.GetExtension(file);
                foreach (string dangerous in DangerousWorkshopExtensions)
                {
                    if (ext.Equals(dangerous, StringComparison.OrdinalIgnoreCase))
                    {
                        DiagnosticsLog.Info(
                            "SteamWorkshop",
                            $"Rejecting workshop install with dangerous file '{file}'");
                        return true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Info("SteamWorkshop", $"Install folder scan failed: {ex.Message}");
        }

        return false;
    }

    private static string? TryFindPreviewFile(string levelId)
    {
        const long MinPreviewBytes = 16;
        // Steam primary preview must be under 1MB.
        const long MaxSteamPreviewBytes = 1024L * 1024L;
        try
        {
            string? levelName = LevelLibrary.GetLevel(levelId)?.Name;
            string? workshopPreview = LevelPreviewManager.TryFindExistingWorkshopPreviewFile(levelId, levelName);
            if (workshopPreview is not null && IsValidPngPreview(workshopPreview, MinPreviewBytes))
            {
                var workshopInfo = new FileInfo(workshopPreview);
                if (workshopInfo.Length <= MaxSteamPreviewBytes)
                {
                    return workshopPreview;
                }

                DiagnosticsLog.Info(
                    "SteamWorkshop",
                    $"Workshop preview too large ({workshopInfo.Length} bytes) path='{workshopPreview}'");
            }

            string previewsRoot = LevelContentPaths.GetPreviewsRoot(LevelIdentity.GetSource(levelId));
            if (!Directory.Exists(previewsRoot))
            {
                return null;
            }

            string stem = levelId.Replace(':', '_');
            foreach (string file in Directory.EnumerateFiles(previewsRoot, "*.png"))
            {
                if (file.EndsWith("_workshop.png", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string name = Path.GetFileNameWithoutExtension(file);
                if (!name.EndsWith(stem, StringComparison.OrdinalIgnoreCase)
                    && !Path.GetFileName(file).Contains(stem, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string fullPath = Path.GetFullPath(file);
                if (!IsValidPngPreview(fullPath, MinPreviewBytes))
                {
                    DiagnosticsLog.Info(
                        "SteamWorkshop",
                        $"Skipping invalid preview level={levelId} path='{fullPath}'");
                    continue;
                }

                var info = new FileInfo(fullPath);
                if (info.Length > MaxSteamPreviewBytes)
                {
                    DiagnosticsLog.Info(
                        "SteamWorkshop",
                        $"Skipping oversized preview ({info.Length} bytes) level={levelId} path='{fullPath}'");
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

    private void ClearWorkshopFieldsFromLocalLevel(LevelMetadata metadata)
    {
        try
        {
            LevelData? data = JsonSerializer.Deserialize<LevelData>(File.ReadAllText(metadata.FilePath), JsonOptions);
            if (data is null)
            {
                return;
            }

            data.WorkshopId = string.Empty;
            data.OwnerSteamId = string.Empty;
            File.WriteAllText(metadata.FilePath, JsonSerializer.Serialize(data, JsonOptions));
            metadata.WorkshopId = string.Empty;
            metadata.OwnerSteamId = string.Empty;
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Info("SteamWorkshop", $"Failed to clear workshop id for {metadata.Id}: {ex.Message}");
            metadata.WorkshopId = string.Empty;
        }
    }

    private static WorkshopPublishResult Fail(string message, bool suggestClearWorkshopId = false) =>
        new() { Success = false, Message = message, SuggestClearWorkshopId = suggestClearWorkshopId };

    private static WorkshopPublishResult Cancelled() =>
        new() { Success = false, WasCancelled = true, Message = "Upload cancelled." };

    // ------------------------------------------------------------------
    // Subscriptions / downloads (read-only Workshop levels)
    // ------------------------------------------------------------------

    public bool IsSubscribed(ulong workshopId)
    {
        if (!IsAvailable || workshopId == 0)
        {
            return false;
        }

        uint state = SteamUGC.GetItemState(new PublishedFileId_t(workshopId));
        return (state & (uint)EItemState.k_EItemStateSubscribed) != 0;
    }

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
                DiagnosticsLog.Info(
                    "SteamWorkshop",
                    $"Subscribe id={workshopId} ok={success} result={result.m_eResult} ioFailure={ioFailure}");
                if (success)
                {
                    RequestDownload(new PublishedFileId_t(workshopId), highPriority: true, reason: "subscribe");
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

        DiagnosticsLog.Info("SteamWorkshop", $"SyncSubscribedItems count={count}");
        var subscribedSet = new HashSet<ulong>();
        foreach (PublishedFileId_t id in subscribed)
        {
            subscribedSet.Add(id.m_PublishedFileId);
            uint state = SteamUGC.GetItemState(id);
            bool installed = (state & (uint)EItemState.k_EItemStateInstalled) != 0;
            bool needsUpdate = (state & (uint)EItemState.k_EItemStateNeedsUpdate) != 0;
            DiagnosticsLog.Info(
                "SteamWorkshop",
                $"Sync id={id.m_PublishedFileId} state={FormatItemState(state)} installed={installed} needsUpdate={needsUpdate}");

            if (installed && !needsUpdate)
            {
                CopyInstalledItem(id);
            }
            else
            {
                RequestDownload(id, highPriority: false, reason: "sync");
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

        DiagnosticsLog.Info(
            "SteamWorkshop",
            $"ItemInstalled id={data.m_nPublishedFileId.m_PublishedFileId}");
        CopyInstalledItem(data.m_nPublishedFileId);
    }

    private void OnDownloadItemResult(DownloadItemResult_t data)
    {
        if (data.m_unAppID != SteamUtils.GetAppID())
        {
            return;
        }

        ulong id = data.m_nPublishedFileId.m_PublishedFileId;
        uint state = SteamUGC.GetItemState(data.m_nPublishedFileId);
        DiagnosticsLog.Info(
            "SteamWorkshop",
            $"DownloadItemResult id={id} result={data.m_eResult} state={FormatItemState(state)}");

        if (data.m_eResult != EResult.k_EResultOK)
        {
            return;
        }

        CopyInstalledItem(data.m_nPublishedFileId);
    }

    private static void RequestDownload(PublishedFileId_t fileId, bool highPriority, string reason)
    {
        uint stateBefore = SteamUGC.GetItemState(fileId);
        bool started = SteamUGC.DownloadItem(fileId, highPriority);
        DiagnosticsLog.Info(
            "SteamWorkshop",
            $"DownloadItem id={fileId.m_PublishedFileId} reason={reason} highPriority={highPriority} "
            + $"started={started} stateBefore={FormatItemState(stateBefore)}");
        if (!started)
        {
            DiagnosticsLog.Info(
                "SteamWorkshop",
                $"DownloadItem refused id={fileId.m_PublishedFileId} — invalid id or user not logged on");
        }
    }

    private static string FormatItemState(uint state)
    {
        if (state == 0)
        {
            return "None";
        }

        var parts = new List<string>();
        if ((state & (uint)EItemState.k_EItemStateSubscribed) != 0) parts.Add("Subscribed");
        if ((state & (uint)EItemState.k_EItemStateLegacyItem) != 0) parts.Add("Legacy");
        if ((state & (uint)EItemState.k_EItemStateInstalled) != 0) parts.Add("Installed");
        if ((state & (uint)EItemState.k_EItemStateNeedsUpdate) != 0) parts.Add("NeedsUpdate");
        if ((state & (uint)EItemState.k_EItemStateDownloading) != 0) parts.Add("Downloading");
        if ((state & (uint)EItemState.k_EItemStateDownloadPending) != 0) parts.Add("DownloadPending");
        if ((state & (uint)EItemState.k_EItemStateDisabledLocally) != 0) parts.Add("DisabledLocally");
        return parts.Count > 0 ? string.Join("|", parts) : $"0x{state:X}";
    }

    private void CopyInstalledItem(PublishedFileId_t fileId)
    {
        if (!SteamUGC.GetItemInstallInfo(fileId, out _, out string installFolder, 1024, out uint updateTimestamp))
        {
            DiagnosticsLog.Info(
                "SteamWorkshop",
                $"GetItemInstallInfo failed id={fileId.m_PublishedFileId} state={FormatItemState(SteamUGC.GetItemState(fileId))}");
            return;
        }

        string sourceLevel = Path.Combine(installFolder, "level.json");
        if (!File.Exists(sourceLevel))
        {
            DiagnosticsLog.Info(
                "SteamWorkshop",
                $"Install folder missing level.json id={fileId.m_PublishedFileId} path='{installFolder}'");
            return;
        }

        if (InstallFolderHasDangerousExtras(installFolder))
        {
            DiagnosticsLog.Info(
                "SteamWorkshop",
                $"Skip sync — dangerous extras in install folder id={fileId.m_PublishedFileId}");
            return;
        }

        var sourceInfo = new FileInfo(sourceLevel);
        if (sourceInfo.Length > LevelValidator.MaxLevelFileBytes)
        {
            DiagnosticsLog.Info(
                "SteamWorkshop",
                $"Skip sync — level.json too large ({sourceInfo.Length} bytes) id={fileId.m_PublishedFileId}");
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

            LevelValidationResult validation = LevelValidator.Validate(data, LevelValidationProfile.Strict);
            if (!validation.IsValid)
            {
                DiagnosticsLog.Info(
                    "SteamWorkshop",
                    $"Skip sync — validation failed id={workshopId}: {validation.Summary}");
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
            AtomicFileWriter.WriteAllText(destinationLevel, JsonSerializer.Serialize(data, JsonOptions));

            string sourcePreview = Path.Combine(installFolder, "preview.png");
            if (File.Exists(sourcePreview) && IsValidPngPreview(sourcePreview, minBytes: 16))
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
