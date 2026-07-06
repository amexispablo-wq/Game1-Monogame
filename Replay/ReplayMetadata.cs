#nullable enable

namespace ColorBlocks.Replay;

public sealed class ReplayFileMetadata
{
  public const int CurrentFormatVersion = 1;

  public int FormatVersion { get; init; } = CurrentFormatVersion;
  public string LevelId { get; init; } = string.Empty;
  public string LevelContentHash { get; init; } = string.Empty;
  public float DurationSeconds { get; init; }
  public int PlayerCount { get; init; }
  public float OfficialBestTime { get; init; }
  public RopeGameplayMode RopeMode { get; init; }
  public bool LavaRiseEnabled { get; init; }
  public int TicksPerSecond { get; init; } = ReplayConstants.DefaultTicksPerSecond;
}

public sealed class ReplayFile
{
  public ReplayFileMetadata Metadata { get; init; } = new();
  public ReplayData Data { get; init; } = new();
}
