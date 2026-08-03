#nullable enable

namespace ColorBlocks.Replay;

public sealed class ReplayFileMetadata
{
  public const int CurrentFormatVersion = 2;

  public int FormatVersion { get; init; } = CurrentFormatVersion;
  public string LevelId { get; init; } = string.Empty;
  public string LevelContentHash { get; init; } = string.Empty;
  /// <summary>SHA256 of serialized <see cref="ReplayData"/> payload. Empty on legacy replays.</summary>
  public string DataChecksum { get; init; } = string.Empty;
  public float DurationSeconds { get; init; }
  public int PlayerCount { get; init; }
  public float OfficialBestTime { get; init; }
  public RopeGameplayMode RopeMode { get; init; }
  public bool LavaRiseEnabled { get; init; }
  public int TicksPerSecond { get; init; } = ReplayConstants.DefaultTicksPerSecond;
  /// <summary>
  /// Focused, unpaused wall-clock seconds for the last timer run. 0 = absent (legacy);
  /// LeaderboardSanity fail-opens the wall-clock ratio check when missing.
  /// </summary>
  public float ActiveWallSeconds { get; init; }
}

public sealed class ReplayFile
{
  public ReplayFileMetadata Metadata { get; init; } = new();
  public ReplayData Data { get; init; } = new();
}
