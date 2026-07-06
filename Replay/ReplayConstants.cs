namespace ColorBlocks.Replay;

public static class ReplayConstants
{
  public const int DefaultTicksPerSecond = 60;
  public const int DefaultBufferSeconds = 60;
  public const int DefaultBufferCapacity = DefaultTicksPerSecond * DefaultBufferSeconds;
  public const int MaxSessionFrames = DefaultTicksPerSecond * 60 * 30;
  public const int HighlightClipMinSeconds = 8;
  public const int HighlightClipMaxSeconds = 12;
  public const int HighlightClipTargetSeconds = 10;
  public const int MaxHighlightClips = 10;
  public const float HighlightTransitionSeconds = 0.5f;
}
