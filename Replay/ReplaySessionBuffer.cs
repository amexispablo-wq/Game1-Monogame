#nullable enable
using System;

namespace ColorBlocks.Replay;

/// <summary>Growing frame store for full gameplay sessions (highscore + highlight analysis).</summary>
public sealed class ReplaySessionBuffer
{
  private ReplayFrame?[] _frames = Array.Empty<ReplayFrame>();
  private int _count;

  public ReplaySessionBuffer(int maxFrames = ReplayConstants.MaxSessionFrames)
  {
    MaxFrames = maxFrames;
  }

  public int MaxFrames { get; }
  public int Count => _count;
  public bool IsFull => _count >= MaxFrames;

  public void Clear()
  {
    for (int i = 0; i < _count; i++)
    {
      _frames[i]?.ReleaseRopeStates();
    }

    _count = 0;
  }

  public void Write(ReplayFrame source)
  {
    if (_count >= MaxFrames)
    {
      return;
    }

    EnsureCapacity(_count + 1);
    ReplayFrame slot = _frames[_count] ??= ReplayFramePool.Rent();
    slot.ReleaseRopeStates();
    slot.CopyFrom(source);
    _count++;
  }

  public void WriteFrom(GameSimulation simulation, Camera camera)
  {
    if (_count >= MaxFrames)
    {
      return;
    }

    EnsureCapacity(_count + 1);
    ReplayFrame slot = _frames[_count] ??= ReplayFramePool.Rent();
    slot.CopyFrom(simulation, camera);
    _count++;
  }

  public ReplayFrame Get(int index)
  {
    if (index < 0 || index >= _count)
    {
      throw new ArgumentOutOfRangeException(nameof(index));
    }

    return _frames[index] ?? throw new InvalidOperationException($"Session frame {index} was not allocated.");
  }

  public void Recycle()
  {
    Clear();
    for (int i = 0; i < _frames.Length; i++)
    {
      if (_frames[i] is ReplayFrame frame)
      {
        ReplayFramePool.Return(frame);
        _frames[i] = null;
      }
    }
  }

  private void EnsureCapacity(int needed)
  {
    if (needed <= _frames.Length)
    {
      return;
    }

    int newSize = _frames.Length == 0
      ? ReplayConstants.SessionBufferInitialCapacity
      : _frames.Length * 2;
    if (newSize < needed)
    {
      newSize = needed;
    }

    if (newSize > MaxFrames)
    {
      newSize = MaxFrames;
    }

    Array.Resize(ref _frames, newSize);
  }
}
