#nullable enable
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ColorBlocks.Replay;

public static class LevelContentHash
{
  public static string ComputeForLevel(string levelId)
  {
    LevelMetadata? metadata = LevelManager.GetLevel(levelId);
    if (metadata is null || !File.Exists(metadata.FilePath))
    {
      return string.Empty;
    }

    try
    {
      byte[] bytes = File.ReadAllBytes(metadata.FilePath);
      byte[] hash = SHA256.HashData(bytes);
      return Convert.ToHexString(hash);
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
