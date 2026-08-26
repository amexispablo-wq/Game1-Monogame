#nullable enable
using System;

namespace ColorBlocks.Replay;

/// <summary>Fixed-capacity ring buffer that overwrites the oldest frame once full.</summary>
public sealed class ReplayBuffer
{
  private readonly ReplayFrame?[] _frames;
  private int _writeIndex;
  private int _count;

  public ReplayBuffer(int capacity)
  {
    Capacity = capacity;
    _frames = new ReplayFrame?[capacity];
  }

  public int Capacity { get; }
  public int Count => _count;
  public bool IsFull => _count >= Capacity;

  public void Clear()
  {
    for (int i = 0; i < _count; i++)
    {
      int index = GetChronologicalIndex(i);
      _frames[index]?.ReleaseRopeStates();
    }

    _writeIndex = 0;
    _count = 0;
  }

  public void Write(ReplayFrame source)
  {
    ReplayFrame slot = _frames[_writeIndex] ??= ReplayFramePool.Rent();
    slot.ReleaseRopeStates();
    slot.CopyFrom(source);
    _writeIndex = (_writeIndex + 1) % Capacity;
    if (_count < Capacity)
    {
      _count++;
    }
  }

  public void WriteFrom(GameSimulation simulation, Camera camera)
  {
    ReplayFrame slot = _frames[_writeIndex] ??= ReplayFramePool.Rent();
    slot.CopyFrom(simulation, camera);
    _writeIndex = (_writeIndex + 1) % Capacity;
    if (_count < Capacity)
    {
      _count++;
    }
  }

  public ReplayFrame GetChronological(int chronologicalIndex)
  {
    ReplayFrame? frame = _frames[GetChronologicalIndex(chronologicalIndex)];
    return frame ?? throw new InvalidOperationException($"Ring frame {chronologicalIndex} was not allocated.");
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

    _writeIndex = 0;
  }

  private int GetChronologicalIndex(int chronologicalIndex)
  {
    if (chronologicalIndex < 0 || chronologicalIndex >= _count)
    {
      throw new ArgumentOutOfRangeException(nameof(chronologicalIndex));
    }

    int start = _count < Capacity ? 0 : _writeIndex;
    return (start + chronologicalIndex) % Capacity;
  }
}
