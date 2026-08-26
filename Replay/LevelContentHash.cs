#nullable enable
using System;
using System.IO;

namespace ColorBlocks.Replay;

public static class LevelContentHash
{
  public static string ComputeForLevel(string levelId)
  {
    LevelMetadata? metadata = LevelLibrary.GetLevel(levelId);
    if (metadata is null || !File.Exists(metadata.FilePath))
    {
      return string.Empty;
    }

    try
    {
      return OfficialLevelManifest.ComputeFileHash(metadata.FilePath);
    }
    catch
    {
      return string.Empty;
    }
  }

  public static bool MatchesCurrentLevel(string levelId, string storedHash)
  {
    if (string.IsNullOrEmpty(storedHash))
    {
      return false;
    }

    return string.Equals(ComputeForLevel(levelId), storedHash, StringComparison.OrdinalIgnoreCase);
  }
}
