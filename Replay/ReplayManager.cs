#nullable enable

namespace ColorBlocks.Replay;

/// <summary>Global in-memory replay services and menu highlight versioning.</summary>
public static class ReplayManager
{
  private static ReplayData? _lastReplay;
  private static int _version;
  private static int _highlightVersion;

  public static int Version => _version;
  public static int HighlightVersion => _highlightVersion;
  public static bool MenuBackgroundEnabled { get; set; } = true;

  public static bool HasReplay() => _lastReplay is not null;

  public static ReplayData? GetReplay() => _lastReplay;

  public static void SaveLastReplay(ReplayData? replay)
  {
    _lastReplay = replay;
    if (replay is not null)
    {
      _version++;
    }
  }

  public static void NotifyHighlightsChanged()
  {
    _highlightVersion++;
  }

  public static void ClearLastReplay()
  {
    _lastReplay = null;
  }
}
