#nullable enable
using System;
using System.IO;
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

    string json = JsonSerializer.Serialize(file, JsonOptions);
    File.WriteAllText(path, json);
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
