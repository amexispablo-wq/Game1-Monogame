#nullable enable

namespace ColorBlocks.Replay;

public enum ReplayRecordingMode
{
  RingBuffer,
  FullSession
}

public sealed class ReplayRecorder
{
  private readonly ReplayBuffer _ringBuffer;
  private readonly ReplaySessionBuffer _sessionBuffer;
  private readonly ReplayFrame _scratchFrame = new();
  private ReplayHeader? _header;
  private ReplayRecordingMode _mode = ReplayRecordingMode.FullSession;
  private bool _isRecording;

  public ReplayRecorder(int ringCapacity = ReplayConstants.DefaultBufferCapacity)
  {
    _ringBuffer = new ReplayBuffer(ringCapacity);
    _sessionBuffer = new ReplaySessionBuffer();
  }

  public bool IsRecording => _isRecording;
  public ReplayRecordingMode Mode => _mode;
  public int RecordedFrameCount => _mode == ReplayRecordingMode.RingBuffer ? _ringBuffer.Count : _sessionBuffer.Count;
  public int BufferCapacity => _mode == ReplayRecordingMode.RingBuffer ? _ringBuffer.Capacity : _sessionBuffer.MaxFrames;
  public float BufferFillPercent => BufferCapacity <= 0 ? 0f : RecordedFrameCount * 100f / BufferCapacity;
  public long FirstRecordedTick => RecordedFrameCount > 0 ? GetFrame(0).Tick : 0;
  public long LastRecordedTick => RecordedFrameCount > 0 ? GetFrame(RecordedFrameCount - 1).Tick : 0;

  public void StartRecording(
    string levelId,
    RopeGameplayMode ropeMode,
    bool lavaRiseEnabled,
    int ticksPerSecond,
    float lavaRiseSpeed,
    float lavaStartSurfaceY,
    ReplayRecordingMode mode = ReplayRecordingMode.FullSession)
  {
    _ringBuffer.Clear();
    _sessionBuffer.Clear();
    _mode = mode;
    _header = new ReplayHeader
    {
      LevelId = levelId,
      RopeMode = ropeMode,
      LavaRiseEnabled = lavaRiseEnabled,
      TicksPerSecond = ticksPerSecond,
      LavaRiseSpeed = lavaRiseSpeed,
      LavaStartSurfaceY = lavaStartSurfaceY
    };
    _isRecording = true;
  }

  public void StopRecording() => _isRecording = false;

  public void ResetSession()
  {
    if (_mode == ReplayRecordingMode.RingBuffer)
    {
      _ringBuffer.Clear();
    }
    else
    {
      _sessionBuffer.Clear();
    }
  }

  public void RecordFrame(GameSimulation simulation, Camera camera)
  {
    if (!_isRecording || _header is null)
    {
      return;
    }

    _scratchFrame.CopyFrom(simulation, camera);
    if (_mode == ReplayRecordingMode.RingBuffer)
    {
      _ringBuffer.Write(_scratchFrame);
    }
    else
    {
      _sessionBuffer.Write(_scratchFrame);
    }
  }

  public ReplayData? ExportReplay()
  {
    if (_header is null || RecordedFrameCount == 0)
    {
      return null;
    }

    ReplayFrameSnapshot[] frames = new ReplayFrameSnapshot[RecordedFrameCount];
    for (int i = 0; i < frames.Length; i++)
    {
      frames[i] = ToSnapshot(GetFrame(i));
    }

    return new ReplayData
    {
      Header = _header,
      Frames = frames
    };
  }

  public ReplayData? ExportRingBufferReplay() => ExportReplayFromRing();

  private ReplayData? ExportReplayFromRing()
  {
    if (_header is null || _ringBuffer.Count == 0)
    {
      return null;
    }

    ReplayFrameSnapshot[] frames = new ReplayFrameSnapshot[_ringBuffer.Count];
    for (int i = 0; i < frames.Length; i++)
    {
      frames[i] = ToSnapshot(_ringBuffer.GetChronological(i));
    }

    return new ReplayData { Header = _header, Frames = frames };
  }

  private ReplayFrame GetFrame(int index)
  {
    return _mode == ReplayRecordingMode.RingBuffer
      ? _ringBuffer.GetChronological(index)
      : _sessionBuffer.Get(index);
  }

  private static ReplayFrameSnapshot ToSnapshot(ReplayFrame frame)
  {
    PlayerSnapshot[] players = new PlayerSnapshot[frame.Players.Count];
    for (int i = 0; i < players.Length; i++)
    {
      players[i] = frame.Players[i];
    }

    RopeSnapshot[] ropes = new RopeSnapshot[frame.Ropes.Count];
    for (int i = 0; i < ropes.Length; i++)
    {
      ropes[i] = frame.Ropes[i].ToSnapshot();
    }

    CheckpointFlagSnapshot[] checkpoints = new CheckpointFlagSnapshot[frame.Checkpoints.Count];
    for (int i = 0; i < checkpoints.Length; i++)
    {
      checkpoints[i] = frame.Checkpoints[i];
    }

    return new ReplayFrameSnapshot
    {
      Tick = frame.Tick,
      Timer = frame.Timer,
      LavaSurfaceY = frame.LavaSurfaceY,
      LavaActive = frame.LavaActive,
      CurrentCheckpointId = frame.CurrentCheckpointId,
      CameraPosition = NetworkVector2.FromVector2(frame.CameraPosition),
      CameraZoom = frame.CameraZoom,
      Players = players,
      Ropes = ropes,
      Checkpoints = checkpoints
    };
  }
}
