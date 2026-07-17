#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

namespace ColorBlocks.Replay;

public static class ReplayStorage
{
  public const string ReplaysFolder = "Replays";
  public const string HighlightsFileName = "Highlights.replay";

  private static readonly Dictionary<string, bool> _bestReplayExistsCache = new(StringComparer.OrdinalIgnoreCase);

  public static bool TryLoadBestReplay(string levelId, out ReplayFile replayFile)
  {
    replayFile = null!;
    ReplayFile? loaded = ReplayFileSerializer.TryLoad(GetBestReplayPath(levelId));
    if (loaded is null)
    {
      InvalidateCache(levelId);
      return false;
    }

    replayFile = loaded;
    _bestReplayExistsCache[levelId] = true;
    return true;
  }

  public static bool HasValidBestReplay(string levelId)
  {
    if (string.IsNullOrEmpty(levelId))
    {
      return false;
    }

    if (_bestReplayExistsCache.TryGetValue(levelId, out bool cached))
    {
      return cached;
    }

    string path = GetBestReplayPath(levelId);
    if (!File.Exists(path))
    {
      _bestReplayExistsCache[levelId] = false;
      return false;
    }

    bool valid = ReplayFileSerializer.TryLoad(path) is not null;
    _bestReplayExistsCache[levelId] = valid;
    return valid;
  }

  public static void SaveBestReplay(ReplayFile file)
  {
    ReplayFileSerializer.Save(GetBestReplayPath(file.Metadata.LevelId), file);
    _bestReplayExistsCache[file.Metadata.LevelId] = true;
  }

  public static void InvalidateBestReplay(string levelId)
  {
    ReplayFileSerializer.TryDelete(GetBestReplayPath(levelId));
    InvalidateCache(levelId);
  }

  public static void DeleteBestReplay(string levelId)
  {
    InvalidateBestReplay(levelId);
  }

  public static void InvalidateCache(string levelId)
  {
    _bestReplayExistsCache.Remove(levelId);
  }

  public static string GetBestReplayPath(string levelId)
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

  private static string SanitizeFileName(string levelId) =>
      levelId.Replace(':', '_');
}
