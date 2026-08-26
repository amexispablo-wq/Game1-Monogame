#nullable enable
using System;
using System.Collections.Generic;

namespace ColorBlocks.Replay;

/// <summary>Cross-run pool so entering a level does not allocate a ReplayFrame per tick.</summary>
internal static class ReplayFramePool
{
  private static readonly Stack<ReplayFrame> Pool = new();

  public static ReplayFrame Rent()
  {
    return Pool.Count > 0 ? Pool.Pop() : new ReplayFrame();
  }

  public static void Return(ReplayFrame frame)
  {
    frame.ReleaseRopeStates();
    Pool.Push(frame);
  }

  public static void Prewarm(int count)
  {
    int needed = Math.Max(0, count - Pool.Count);
    for (int i = 0; i < needed; i++)
    {
      Pool.Push(new ReplayFrame());
    }
  }
}
