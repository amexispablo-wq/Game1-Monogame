#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace ColorBlocks.Replay;

/// <summary>
/// One simulation tick of state required for deterministic visual playback.
/// Mutable storage reused by <see cref="ReplayBuffer"/> to avoid per-tick allocations.
/// </summary>
public sealed class ReplayFrame
{
  private readonly List<PlayerSnapshot> _players = new(4);
  private readonly List<ReplayRopeState> _ropes = new(3);
  private readonly List<CheckpointFlagSnapshot> _checkpoints = new(8);

  public long Tick { get; private set; }
  public TimerSnapshot Timer { get; private set; }
  public float LavaSurfaceY { get; private set; }
  public bool LavaActive { get; private set; }
  public int? CurrentCheckpointId { get; private set; }
  public Vector2 CameraPosition { get; private set; }
  public float CameraZoom { get; private set; }

  public IReadOnlyList<PlayerSnapshot> Players => _players;
  public IReadOnlyList<ReplayRopeState> Ropes => _ropes;
  public IReadOnlyList<CheckpointFlagSnapshot> Checkpoints => _checkpoints;

  public void CopyFrom(GameSimulation simulation, Camera camera)
  {
    GameSnapshot snapshot = simulation.LastSnapshot;
    Tick = snapshot.Tick;
    Timer = snapshot.Timer;
    LavaSurfaceY = simulation.LavaSurfaceY;
    LavaActive = simulation.LavaActive;
    CurrentCheckpointId = simulation.PlayerManager.CurrentCheckpointId;
    CameraPosition = camera.Position;
    CameraZoom = camera.Zoom;

    _players.Clear();
    foreach (PlayerSnapshot playerSnapshot in snapshot.Players)
    {
      _players.Add(playerSnapshot);
    }

    _ropes.Clear();
    foreach (Rope rope in simulation.Ropes)
    {
      ReplayRopeState ropeState = ReplayRopeState.Rent();
      ropeState.CopyFrom(rope);
      _ropes.Add(ropeState);
    }

    _checkpoints.Clear();
    foreach (CheckpointFlag checkpoint in simulation.Level.CheckpointFlags)
    {
      _checkpoints.Add(new CheckpointFlagSnapshot(
        checkpoint.Id,
        checkpoint.Position.X,
        checkpoint.Position.Y,
        checkpoint.IsActive));
    }
  }

  public void CopyFrom(ReplayFrame other)
  {
    Tick = other.Tick;
    Timer = other.Timer;
    LavaSurfaceY = other.LavaSurfaceY;
    LavaActive = other.LavaActive;
    CurrentCheckpointId = other.CurrentCheckpointId;
    CameraPosition = other.CameraPosition;
    CameraZoom = other.CameraZoom;

    _players.Clear();
    foreach (PlayerSnapshot playerSnapshot in other._players)
    {
      _players.Add(playerSnapshot);
    }

    _ropes.Clear();
    foreach (ReplayRopeState ropeState in other._ropes)
    {
      ReplayRopeState copy = ReplayRopeState.Rent();
      copy.CopyFrom(ropeState);
      _ropes.Add(copy);
    }

    _checkpoints.Clear();
    foreach (CheckpointFlagSnapshot checkpoint in other._checkpoints)
    {
      _checkpoints.Add(checkpoint);
    }
  }

  public void ReleaseRopeStates()
  {
    foreach (ReplayRopeState ropeState in _ropes)
    {
      ReplayRopeState.Return(ropeState);
    }

    _ropes.Clear();
  }
}

/// <summary>Mutable rope state for ring-buffer frames without allocating <see cref="RopeSnapshot"/> lists.</summary>
public sealed class ReplayRopeState
{
  private static readonly Stack<ReplayRopeState> Pool = new();

  private readonly List<NetworkVector2> _nodePositions = new(32);

  public int NetworkId { get; private set; }
  public int OwnerId { get; private set; }
  public int StartPlayerNetworkId { get; private set; }
  public int EndPlayerNetworkId { get; private set; }
  public RopeGameplayMode RopeMode { get; private set; }
  public IReadOnlyList<NetworkVector2> NodePositions => _nodePositions;
  public float Tension { get; private set; }
  public bool IsTense { get; private set; }
  public float PullIntensity { get; private set; }
  public int PulledNodeCount { get; private set; }

  public static ReplayRopeState Rent()
  {
    if (Pool.Count > 0)
    {
      return Pool.Pop();
    }

    return new ReplayRopeState();
  }

  public static void Return(ReplayRopeState state)
  {
    state._nodePositions.Clear();
    Pool.Push(state);
  }

  public void CopyFrom(Rope rope)
  {
    NetworkId = rope.NetworkId;
    OwnerId = rope.OwnerId;
    StartPlayerNetworkId = rope.StartPlayer.NetworkId;
    EndPlayerNetworkId = rope.EndPlayer.NetworkId;
    RopeMode = rope.GameplayMode;
    Tension = rope.LastTension;
    IsTense = rope.IsTense;
    PullIntensity = rope.LastPullIntensity;
    PulledNodeCount = rope.LastPulledNodeCount;

    _nodePositions.Clear();
    foreach (RopeNode node in rope.Nodes)
    {
      _nodePositions.Add(NetworkVector2.FromVector2(node.Position));
    }
  }

  public void CopyFrom(ReplayRopeState other)
  {
    NetworkId = other.NetworkId;
    OwnerId = other.OwnerId;
    StartPlayerNetworkId = other.StartPlayerNetworkId;
    EndPlayerNetworkId = other.EndPlayerNetworkId;
    RopeMode = other.RopeMode;
    Tension = other.Tension;
    IsTense = other.IsTense;
    PullIntensity = other.PullIntensity;
    PulledNodeCount = other.PulledNodeCount;

    _nodePositions.Clear();
    foreach (NetworkVector2 position in other._nodePositions)
    {
      _nodePositions.Add(position);
    }
  }

  public RopeSnapshot ToSnapshot()
  {
    RopeSnapshot snapshot = new()
    {
      NetworkId = NetworkId,
      OwnerId = OwnerId,
      StartPlayerNetworkId = StartPlayerNetworkId,
      EndPlayerNetworkId = EndPlayerNetworkId,
      RopeMode = RopeMode,
      Tension = Tension,
      IsTense = IsTense,
      PullIntensity = PullIntensity,
      PulledNodeCount = PulledNodeCount
    };

    foreach (NetworkVector2 position in _nodePositions)
    {
      snapshot.NodePositions.Add(position);
    }

    return snapshot;
  }

  public void ApplyTo(Rope rope)
  {
    RopeSnapshot snapshot = ToSnapshot();
    rope.ApplySnapshot(snapshot);
  }
}
