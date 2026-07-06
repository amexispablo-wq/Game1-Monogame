#nullable enable
using System;

namespace ColorBlocks.Replay;

/// <summary>Immutable replay export ready for playback, disk persistence, or sharing.</summary>
public sealed class ReplayData
{
  public ReplayHeader Header { get; init; } = new();
  public ReplayFrameSnapshot[] Frames { get; init; } = Array.Empty<ReplayFrameSnapshot>();
}

public sealed class ReplayHeader
{
  public string LevelId { get; init; } = string.Empty;
  public RopeGameplayMode RopeMode { get; init; }
  public bool LavaRiseEnabled { get; init; }
  public int TicksPerSecond { get; init; } = ReplayConstants.DefaultTicksPerSecond;
  public float LavaRiseSpeed { get; init; }
  public float LavaStartSurfaceY { get; init; }
}

/// <summary>Immutable per-frame snapshot stored in exported <see cref="ReplayData"/>.</summary>
public sealed class ReplayFrameSnapshot
{
  public long Tick { get; init; }
  public TimerSnapshot Timer { get; init; }
  public float LavaSurfaceY { get; init; }
  public bool LavaActive { get; init; }
  public int? CurrentCheckpointId { get; init; }
  public NetworkVector2 CameraPosition { get; init; }
  public float CameraZoom { get; init; } = 1f;
  public PlayerSnapshot[] Players { get; init; } = Array.Empty<PlayerSnapshot>();
  public RopeSnapshot[] Ropes { get; init; } = Array.Empty<RopeSnapshot>();
  public CheckpointFlagSnapshot[] Checkpoints { get; init; } = Array.Empty<CheckpointFlagSnapshot>();
}
