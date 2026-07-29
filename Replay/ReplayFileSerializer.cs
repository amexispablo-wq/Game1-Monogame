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
    // Death → restart-from-start used to leave pre-restart frames in the buffer, so
    // DurationSeconds (frame count) could exceed the official timer. Trim to the last
    // contiguous timer run before writing a best-replay / Steam upload payload.
    ReplayData trimmed = TrimToLastTimerRun(data);
    int ticksPerSecond = Math.Max(1, trimmed.Header.TicksPerSecond);
    float roundedBest = BestTimeStorage.RoundToTenThousandths(officialBestTime);
    return new ReplayFile
    {
      Metadata = new ReplayFileMetadata
      {
        FormatVersion = ReplayFileMetadata.CurrentFormatVersion,
        LevelId = levelId,
        LevelContentHash = LevelContentHash.ComputeForLevel(levelId),
        DataChecksum = ComputeDataChecksum(trimmed),
        DurationSeconds = roundedBest,
        PlayerCount = playerCount,
        OfficialBestTime = roundedBest,
        RopeMode = trimmed.Header.RopeMode,
        LavaRiseEnabled = trimmed.Header.LavaRiseEnabled,
        TicksPerSecond = ticksPerSecond
      },
      Data = trimmed
    };
  }

  /// <summary>
  /// Drops frames recorded before the last timer restart (ElapsedTime drop after a
  /// non-trivial value). Keeps a clean run when the player died and restarted from start
  /// without clearing the session buffer.
  /// </summary>
  public static ReplayData TrimToLastTimerRun(ReplayData data)
  {
    ReplayFrameSnapshot[] frames = data.Frames;
    if (frames.Length <= 1)
    {
      return data;
    }

    int start = 0;
    float previousElapsed = frames[0].Timer.ElapsedTime;
    for (int i = 1; i < frames.Length; i++)
    {
      float elapsed = frames[i].Timer.ElapsedTime;
      if (previousElapsed > 0.5f && elapsed < 0.05f)
      {
        start = i;
      }

      previousElapsed = elapsed;
    }

    if (start <= 0)
    {
      return data;
    }

    var trimmedFrames = new ReplayFrameSnapshot[frames.Length - start];
    Array.Copy(frames, start, trimmedFrames, 0, trimmedFrames.Length);
    DiagnosticsLog.Info(
      "Replay",
      $"Trimmed {start} pre-restart frames ({frames.Length} → {trimmedFrames.Length})");
    return new ReplayData
    {
      Header = data.Header,
      Frames = trimmedFrames
    };
  }

  /// <summary>
  /// Rewrites a best-replay whose DurationSeconds diverges from the official score
  /// (legacy death-restart recordings). Returns false when the file cannot be made valid.
  /// </summary>
  public static bool TryRepairDurationMismatch(string path, float expectedScoreSeconds, out string reason)
  {
    reason = string.Empty;
    if (!File.Exists(path))
    {
      reason = "Official PB upload requires a best-replay file.";
      return false;
    }

    ReplayFile? file = TryLoad(path, invalidateOnHashMismatch: false);
    if (file is null)
    {
      reason = "Best-replay failed integrity checks.";
      return false;
    }

    float roundedScore = BestTimeStorage.RoundToTenThousandths(expectedScoreSeconds);
    float durationDelta = MathF.Abs(file.Metadata.DurationSeconds - roundedScore);
    if (durationDelta <= LeaderboardSanity.ReplayDurationToleranceSeconds)
    {
      return true;
    }

    ReplayData trimmed = TrimToLastTimerRun(file.Data);
    if (trimmed.Frames.Length == 0)
    {
      reason = "Best-replay has no frames after trim.";
      return false;
    }

    float lastElapsed = trimmed.Frames[^1].Timer.IsComplete
      ? trimmed.Frames[^1].Timer.FinalTime
      : trimmed.Frames[^1].Timer.ElapsedTime;
    float lastDelta = MathF.Abs(BestTimeStorage.RoundToTenThousandths(lastElapsed) - roundedScore);
    if (lastDelta > LeaderboardSanity.ReplayDurationToleranceSeconds)
    {
      reason =
        $"Replay duration {file.Metadata.DurationSeconds:F2}s diverges from score {roundedScore:F2}s";
      return false;
    }

    var repaired = new ReplayFile
    {
      Metadata = new ReplayFileMetadata
      {
        FormatVersion = file.Metadata.FormatVersion,
        LevelId = file.Metadata.LevelId,
        LevelContentHash = file.Metadata.LevelContentHash,
        DataChecksum = ComputeDataChecksum(trimmed),
        DurationSeconds = roundedScore,
        PlayerCount = file.Metadata.PlayerCount,
        OfficialBestTime = roundedScore,
        RopeMode = file.Metadata.RopeMode,
        LavaRiseEnabled = file.Metadata.LavaRiseEnabled,
        TicksPerSecond = file.Metadata.TicksPerSecond
      },
      Data = trimmed
    };

    Save(path, repaired);
    DiagnosticsLog.Info(
      "Replay",
      $"Repaired duration mismatch for '{path}' ({file.Metadata.DurationSeconds:F2}s → {roundedScore:F2}s)");
    return true;
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
