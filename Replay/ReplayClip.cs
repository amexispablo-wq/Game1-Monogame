#nullable enable

namespace ColorBlocks.Replay;

public sealed class ReplayClip
{
  public string Type { get; init; } = string.Empty;
  public float Score { get; init; }
  public float DurationSeconds { get; init; }
  public int StartFrameIndex { get; init; }
  public int EndFrameIndex { get; init; }
  public string LevelId { get; init; } = string.Empty;
}

public sealed class HighlightClipEntry
{
  public string Type { get; init; } = string.Empty;
  public float Score { get; init; }
  public float DurationSeconds { get; init; }
  public string LevelId { get; init; } = string.Empty;
  public int StartFrameIndex { get; init; }
  public int EndFrameIndex { get; init; }
  public ReplayData ClipData { get; init; } = new();
}

public sealed class HighlightReplayFile
{
  public const int CurrentFormatVersion = 1;

  public int FormatVersion { get; init; } = CurrentFormatVersion;
  public HighlightClipEntry[] Clips { get; init; } = [];
}
