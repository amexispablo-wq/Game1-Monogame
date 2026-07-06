#nullable enable

namespace ColorBlocks.Replay;

/// <summary>Runtime replay diagnostics for F3 debug overlay.</summary>
public static class ReplayDiagnostics
{
  public static ReplayRecorder? ActiveRecorder { get; set; }
  public static ReplayPlayer? ActivePlayer { get; set; }
  public static bool DebugOverlayVisible { get; set; }
}
