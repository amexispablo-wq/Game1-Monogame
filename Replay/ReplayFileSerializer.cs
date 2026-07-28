#nullable enable
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColorBlocks.Replay;

public static class ReplayFileSerializer
{
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    WriteIndented = false,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
  };

  static ReplayFileSerializer()
  {
    JsonOptions.Converters.Add(new JsonStringEnumConverter());
  }

  public static ReplayFile CreateFromSession(
    ReplayData data,
    string levelId,
    float officialBestTime,
    int playerCount)
  {
    int ticksPerSecond = Math.Max(1, data.Header.TicksPerSecond);
    return new ReplayFile
    {
      Metadata = new ReplayFileMetadata
      {
        FormatVersion = ReplayFileMetadata.CurrentFormatVersion,
        LevelId = levelId,
        LevelContentHash = LevelContentHash.ComputeForLevel(levelId),
        DataChecksum = ComputeDataChecksum(data),
        DurationSeconds = data.Frames.Length / (float)ticksPerSecond,
        PlayerCount = playerCount,
        OfficialBestTime = officialBestTime,
        RopeMode = data.Header.RopeMode,
        LavaRiseEnabled = data.Header.LavaRiseEnabled,
        TicksPerSecond = ticksPerSecond
      },
      Data = data
    };
  }

  public static void Save(string path, ReplayFile file)
  {
    string? directory = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(directory))
    {
      Directory.CreateDirectory(directory);
    }

    // Ensure checksum is present/fresh before writing.
    string checksum = string.IsNullOrEmpty(file.Metadata.DataChecksum)
      ? ComputeDataChecksum(file.Data)
      : file.Metadata.DataChecksum;
    var toWrite = new ReplayFile
    {
      Metadata = new ReplayFileMetadata
      {
        FormatVersion = file.Metadata.FormatVersion,
        LevelId = file.Metadata.LevelId,
        LevelContentHash = file.Metadata.LevelContentHash,
        DataChecksum = checksum,
        DurationSeconds = file.Metadata.DurationSeconds,
        PlayerCount = file.Metadata.PlayerCount,
        OfficialBestTime = file.Metadata.OfficialBestTime,
        RopeMode = file.Metadata.RopeMode,
        LavaRiseEnabled = file.Metadata.LavaRiseEnabled,
        TicksPerSecond = file.Metadata.TicksPerSecond
      },
      Data = file.Data
    };

    string json = JsonSerializer.Serialize(toWrite, JsonOptions);
    AtomicFileWriter.WriteAllText(path, json);
  }

  public static ReplayFile? TryLoad(string path, bool invalidateOnHashMismatch = true)
  {
    if (!File.Exists(path))
    {
      return null;
    }

    try
    {
      string json = File.ReadAllText(path);
      ReplayFile? file = JsonSerializer.Deserialize<ReplayFile>(json, JsonOptions);
      if (file is null)
      {
        return null;
      }

      if (!string.IsNullOrEmpty(file.Metadata.DataChecksum)
          && !VerifyDataChecksum(file))
      {
        if (invalidateOnHashMismatch)
        {
          TryDelete(path);
          return null;
        }

        DiagnosticsLog.Info("Replay", $"Loaded replay with data-checksum mismatch (kept): '{path}'");
      }

      if (!LevelContentHash.MatchesCurrentLevel(file.Metadata.LevelId, file.Metadata.LevelContentHash))
      {
        if (invalidateOnHashMismatch)
        {
          TryDelete(path);
          return null;
        }

        // World-record downloads from other players can hash-mismatch across builds;
        // keep the file so ghosts/WR viewer still work.
        DiagnosticsLog.Info(
          "Replay",
          $"Loaded replay with level-hash mismatch (kept): '{path}' level={file.Metadata.LevelId}");
      }

      return file;
    }
    catch
    {
      return null;
    }
  }

  public static string ComputeDataChecksum(ReplayData data)
  {
    byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data, JsonOptions));
    return Convert.ToHexString(SHA256.HashData(bytes));
  }

  public static bool VerifyDataChecksum(ReplayFile file)
  {
    if (string.IsNullOrEmpty(file.Metadata.DataChecksum))
    {
      return true;
    }

    string actual = ComputeDataChecksum(file.Data);
    return string.Equals(actual, file.Metadata.DataChecksum, StringComparison.OrdinalIgnoreCase);
  }

  public static void TryDelete(string path)
  {
    try
    {
      if (File.Exists(path))
      {
        File.Delete(path);
      }
    }
    catch
    {
    }
  }
}
