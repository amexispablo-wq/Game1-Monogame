#nullable enable
using System;

namespace ColorBlocks.Replay;

/// <summary>Growing frame store for full gameplay sessions (highscore + highlight analysis).</summary>
public sealed class ReplaySessionBuffer
{
  private readonly ReplayFrame[] _frames;
  private int _count;

  public ReplaySessionBuffer(int maxFrames = ReplayConstants.MaxSessionFrames)
  {
    MaxFrames = maxFrames;
    _frames = new ReplayFrame[maxFrames];
    for (int i = 0; i < maxFrames; i++)
    {
      _frames[i] = new ReplayFrame();
    }
  }

  public int MaxFrames { get; }
  public int Count => _count;
  public bool IsFull => _count >= MaxFrames;

  public void Clear()
  {
    for (int i = 0; i < _count; i++)
    {
      _frames[i].ReleaseRopeStates();
    }

    _count = 0;
  }

  public void Write(ReplayFrame source)
  {
    if (_count >= MaxFrames)
    {
      return;
    }

    _frames[_count].ReleaseRopeStates();
    _frames[_count].CopyFrom(source);
    _count++;
  }

  public ReplayFrame Get(int index)
  {
    if (index < 0 || index >= _count)
    {
      throw new ArgumentOutOfRangeException(nameof(index));
    }

    return _frames[index];
  }
}
