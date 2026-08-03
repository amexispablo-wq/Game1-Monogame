#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Steamworks;

namespace ColorBlocks;

/// <summary>
/// Client-side workshop leaderboard eligibility: top N by unique subscriptions via Steam UGC
/// (no publisher key / no backend). Official levels are always eligible.
/// </summary>
public sealed class WorkshopLeaderboardEligibility
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly object _gate = new();
    private readonly HashSet<ulong> _topIds = new();
    private DateTime _fetchedUtc = DateTime.MinValue;
    private bool _fetchInFlight;
    private uint _nextPage = 1;
    private readonly List<ulong> _building = new();

    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromMinutes(45);

    public bool HasCache
    {
        get
        {
            lock (_gate)
            {
                return _topIds.Count > 0;
            }
        }
    }

    public void EnsureFresh()
    {
        if (!SteamAPI.IsSteamRunning())
        {
            TryLoadDiskCache();
            return;
        }

        lock (_gate)
        {
            if (_fetchInFlight)
            {
                return;
            }

            if (_topIds.Count > 0 && DateTime.UtcNow - _fetchedUtc < CacheTtl)
            {
                return;
            }

            _fetchInFlight = true;
            _nextPage = 1;
            _building.Clear();
        }

        TryLoadDiskCache();
        RequestNextPage();
    }

    public bool IsLevelEligible(string levelId)
    {
        if (!LevelIdentity.TryParse(levelId, out LevelSource source, out string fileStem))
        {
            return false;
        }

        if (source == LevelSource.Official)
        {
            return true;
        }

        if (source != LevelSource.Workshop || !ulong.TryParse(fileStem, out ulong workshopId))
        {
            return false;
        }

        EnsureFresh();
        lock (_gate)
        {
            return _topIds.Contains(workshopId);
        }
    }

    private void RequestNextPage()
    {
        AppId_t appId = SteamUtils.GetAppID();
        UGCQueryHandle_t query = SteamUGC.CreateQueryAllUGCRequest(
            EUGCQuery.k_EUGCQuery_RankedByTotalUniqueSubscriptions,
            EUGCMatchingUGCType.k_EUGCMatchingUGCType_Items,
            appId,
            appId,
            _nextPage);

        if (query == UGCQueryHandle_t.Invalid)
        {
            FinishFetch(success: false);
            return;
        }

        SteamCallTracker.Track<SteamUGCQueryCompleted_t>(
            SteamUGC.SendQueryUGCRequest(query),
            (result, ioFailure) =>
            {
                uint page = _nextPage;
                try
                {
                    if (ioFailure || result.m_eResult != EResult.k_EResultOK)
                    {
                        DiagnosticsLog.Info(
                            "WorkshopLBEligibility",
                            $"UGC query page={page} failed io={ioFailure} result={result.m_eResult}");
                        FinishFetch(success: _building.Count > 0);
                        return;
                    }

                    for (uint i = 0; i < result.m_unNumResultsReturned; i++)
                    {
                        if (!SteamUGC.GetQueryUGCResult(result.m_handle, i, out SteamUGCDetails_t details))
                        {
                            continue;
                        }

                        ulong id = details.m_nPublishedFileId.m_PublishedFileId;
                        if (id != 0)
                        {
                            _building.Add(id);
                        }

                        if (_building.Count >= LeaderboardQuota.WorkshopEligibleLevelCap)
                        {
                            break;
                        }
                    }

                    bool more =
                        result.m_unNumResultsReturned > 0
                        && _building.Count < LeaderboardQuota.WorkshopEligibleLevelCap
                        && page < 200;

                    if (more)
                    {
                        _nextPage = page + 1;
                        RequestNextPage();
                        return;
                    }

                    FinishFetch(success: true);
                }
                finally
                {
                    SteamUGC.ReleaseQueryUGCRequest(result.m_handle);
                }
            });
    }

    private void FinishFetch(bool success)
    {
        lock (_gate)
        {
            if (success && _building.Count > 0)
            {
                _topIds.Clear();
                int take = Math.Min(LeaderboardQuota.WorkshopEligibleLevelCap, _building.Count);
                for (int i = 0; i < take; i++)
                {
                    _topIds.Add(_building[i]);
                }

                _fetchedUtc = DateTime.UtcNow;
                TrySaveDiskCache();
                DiagnosticsLog.Info("WorkshopLBEligibility", $"Top cache refreshed count={_topIds.Count}");
            }
            else if (_topIds.Count == 0)
            {
                DiagnosticsLog.Info("WorkshopLBEligibility", "Top cache refresh failed; using disk/empty.");
            }

            _fetchInFlight = false;
            _building.Clear();
        }
    }

    private static string CachePath =>
        Path.Combine(UserDataPaths.Cache, "workshop-leaderboard-top.json");

    private void TrySaveDiskCache()
    {
        try
        {
            Directory.CreateDirectory(UserDataPaths.Cache);
            var dto = new CacheDto
            {
                Ids = new List<ulong>(_topIds),
                GeneratedUtc = _fetchedUtc
            };
            File.WriteAllText(CachePath, JsonSerializer.Serialize(dto, JsonOptions));
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Info("WorkshopLBEligibility", $"Cache write failed: {ex.Message}");
        }
    }

    private void TryLoadDiskCache()
    {
        try
        {
            if (!File.Exists(CachePath))
            {
                return;
            }

            CacheDto? dto = JsonSerializer.Deserialize<CacheDto>(File.ReadAllText(CachePath), JsonOptions);
            if (dto?.Ids is null || dto.Ids.Count == 0)
            {
                return;
            }

            lock (_gate)
            {
                if (_topIds.Count > 0)
                {
                    return;
                }

                foreach (ulong id in dto.Ids)
                {
                    _topIds.Add(id);
                }

                _fetchedUtc = dto.GeneratedUtc == default ? DateTime.UtcNow : dto.GeneratedUtc;
            }

            DiagnosticsLog.Info("WorkshopLBEligibility", $"Top cache loaded from disk count={dto.Ids.Count}");
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Info("WorkshopLBEligibility", $"Cache read failed: {ex.Message}");
        }
    }

    private sealed class CacheDto
    {
        [JsonPropertyName("ids")]
        public List<ulong>? Ids { get; set; }

        [JsonPropertyName("generatedUtc")]
        public DateTime GeneratedUtc { get; set; }
    }
}
