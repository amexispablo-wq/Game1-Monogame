#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

namespace ColorBlocks.Replay;

public static class ReplayStorage
{
  public const string ReplaysFolder = "Replays";
  public const string HighlightsFileName = "Highlights.replay";
  private const int MaxPlayerCounts = 4;

  private static readonly Dictionary<string, bool> _bestReplayExistsCache = new(StringComparer.OrdinalIgnoreCase);

  public static bool TryLoadBestReplay(string levelId, int playerCount, out ReplayFile replayFile)
  {
    replayFile = null!;
    int clamped = ClampPlayerCount(playerCount);
    ReplayFile? loaded = ReplayFileSerializer.TryLoad(GetBestReplayPath(levelId, clamped));
    if (loaded is null && clamped == 1)
    {
      // Legacy unscoped PB path.
      loaded = ReplayFileSerializer.TryLoad(GetLegacyBestReplayPath(levelId));
    }

    if (loaded is null)
    {
      InvalidateCache(levelId, clamped);
      return false;
    }

    replayFile = loaded;
    _bestReplayExistsCache[CacheKey(levelId, clamped)] = true;
    return true;
  }

  public static bool HasValidBestReplay(string levelId, int playerCount)
  {
    if (string.IsNullOrEmpty(levelId))
    {
      return false;
    }

    int clamped = ClampPlayerCount(playerCount);
    string key = CacheKey(levelId, clamped);
    if (_bestReplayExistsCache.TryGetValue(key, out bool cached))
    {
      return cached;
    }

    string path = GetBestReplayPath(levelId, clamped);
    if (!File.Exists(path) && clamped == 1)
    {
      path = GetLegacyBestReplayPath(levelId);
    }

    if (!File.Exists(path))
    {
      _bestReplayExistsCache[key] = false;
      return false;
    }

    bool valid = ReplayFileSerializer.TryLoad(path) is not null;
    _bestReplayExistsCache[key] = valid;
    return valid;
  }

  public static void SaveBestReplay(ReplayFile file, int playerCount)
  {
    int clamped = ClampPlayerCount(playerCount);
    ReplayFileSerializer.Save(GetBestReplayPath(file.Metadata.LevelId, clamped), file);
    _bestReplayExistsCache[CacheKey(file.Metadata.LevelId, clamped)] = true;
  }

  public static void InvalidateBestReplay(string levelId)
  {
    for (int playerCount = 1; playerCount <= MaxPlayerCounts; playerCount++)
    {
      ReplayFileSerializer.TryDelete(GetBestReplayPath(levelId, playerCount));
      InvalidateCache(levelId, playerCount);
    }

    ReplayFileSerializer.TryDelete(GetLegacyBestReplayPath(levelId));
  }

  public static void InvalidateBestReplay(string levelId, int playerCount)
  {
    int clamped = ClampPlayerCount(playerCount);
    ReplayFileSerializer.TryDelete(GetBestReplayPath(levelId, clamped));
    if (clamped == 1)
    {
      ReplayFileSerializer.TryDelete(GetLegacyBestReplayPath(levelId));
    }

    InvalidateCache(levelId, clamped);
  }

  public static void DeleteBestReplay(string levelId)
  {
    InvalidateBestReplay(levelId);
  }

  public static void InvalidateCache(string levelId)
  {
    for (int playerCount = 1; playerCount <= MaxPlayerCounts; playerCount++)
    {
      _bestReplayExistsCache.Remove(CacheKey(levelId, playerCount));
    }
  }

  public static void InvalidateCache(string levelId, int playerCount)
  {
    _bestReplayExistsCache.Remove(CacheKey(levelId, ClampPlayerCount(playerCount)));
  }

  public static string GetBestReplayPath(string levelId, int playerCount)
  {
    LevelSource source = LevelIdentity.GetSource(levelId);
    int clamped = ClampPlayerCount(playerCount);
    string fileName = $"{SanitizeFileName(levelId)}_Best_p{clamped}.replay";
    return Path.Combine(UserDataPaths.GetGhostsRoot(source), fileName);
  }

  private static string GetLegacyBestReplayPath(string levelId)
  {
    LevelSource source = LevelIdentity.GetSource(levelId);
    string fileName = $"{SanitizeFileName(levelId)}_Best.replay";
    return Path.Combine(UserDataPaths.GetGhostsRoot(source), fileName);
  }

  public static string GetHighlightsPath(LevelSource source)
  {
    return Path.Combine(UserDataPaths.GetHighlightsRoot(source), HighlightsFileName);
  }

  public static string GetHighlightsPath(string levelId)
  {
    return GetHighlightsPath(LevelIdentity.GetSource(levelId));
  }

  public static string GetReplaysDirectory(LevelSource source)
  {
    return LevelContentPaths.GetReplaysRoot(source);
  }

  public static string GetReplaysDirectory()
  {
    return LevelContentPaths.GetReplaysRoot(LevelSource.Local);
  }

  private static int ClampPlayerCount(int playerCount) => Math.Clamp(playerCount, 1, MaxPlayerCounts);

  private static string CacheKey(string levelId, int playerCount) => $"{levelId}|p{playerCount}";

  private static string SanitizeFileName(string levelId) =>
      levelId.Replace(':', '_');
}
