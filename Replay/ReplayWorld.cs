#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace ColorBlocks.Replay;

/// <summary>
/// Lightweight world used only for replay playback. No physics, input, or AI.
/// </summary>
public sealed class ReplayWorld
{
  public ReplayWorld(
    Level level,
    PlayerManager playerManager,
    PhysicsWorld physicsWorld,
    ReplayHeader header)
  {
    Level = level;
    PlayerManager = playerManager;
    PhysicsWorld = physicsWorld;
    Header = header;
    LavaActive = level.Lava is not null;
    LavaSurfaceY = header.LavaStartSurfaceY;
  }

  public Level Level { get; }
  public PlayerManager PlayerManager { get; }
  public PhysicsWorld PhysicsWorld { get; }
  public ReplayHeader Header { get; }
  public IReadOnlyList<Player> Players => PlayerManager.Players;
  public IReadOnlyList<Rope> Ropes => PhysicsWorld.Ropes;
  public float ElapsedTime { get; private set; }
  public float LavaSurfaceY { get; private set; }
  public bool LavaActive { get; }

  public static ReplayWorld Create(ReplayData data)
  {
    if (data.Frames.Length == 0)
    {
      throw new InvalidOperationException("ReplayData has no frames.");
    }

    ReplayHeader header = data.Header;
    Level level = LevelManager.LoadLevel(header.LevelId);
    GameSession session = GameSession.CreateLocalTest(header.LevelId, header.RopeMode);
    session.LavaRiseEnabled = header.LavaRiseEnabled;

    PlayerManager playerManager = new(session, level);
    ReplayWorldFactory.SpawnPlayersFromFirstFrame(playerManager, session, data.Frames[0]);

    PhysicsWorld physicsWorld = new(level, playerManager.Players, session, header.RopeMode);
    ReplayWorldFactory.ConfigureRopes(physicsWorld, data.Frames[0]);

    ReplayWorld world = new(level, playerManager, physicsWorld, header);
    world.ApplyFrame(data.Frames[0]);
    return world;
  }

  public void ApplyFrame(ReplayFrameSnapshot frame)
  {
    foreach (PlayerSnapshot playerSnapshot in frame.Players)
    {
      Player? player = FindPlayer(playerSnapshot.NetworkId);
      player?.ApplySnapshot(playerSnapshot);
    }

    foreach (RopeSnapshot ropeSnapshot in frame.Ropes)
    {
      Rope? rope = FindRope(ropeSnapshot.NetworkId);
      rope?.ApplySnapshot(ropeSnapshot);
    }

    foreach (Rope rope in Ropes)
    {
      rope.RefreshVisualState();
    }

    ElapsedTime = frame.Timer.ElapsedTime;
    LavaSurfaceY = frame.LavaSurfaceY;

    foreach (CheckpointFlag checkpoint in Level.CheckpointFlags)
    {
      checkpoint.IsActive = false;
    }

    PlayerManager.ClearCheckpoint();
    foreach (CheckpointFlagSnapshot checkpointSnapshot in frame.Checkpoints)
    {
      CheckpointFlag? checkpoint = FindCheckpoint(checkpointSnapshot.Id);
      if (checkpoint is null)
      {
        continue;
      }

      checkpoint.IsActive = checkpointSnapshot.IsActive;
      if (checkpointSnapshot.IsActive && frame.CurrentCheckpointId == checkpointSnapshot.Id)
      {
        PlayerManager.ActivateCheckpoint(checkpoint);
      }
    }
  }

  private Player? FindPlayer(int networkId)
  {
    foreach (Player player in Players)
    {
      if (player.NetworkId == networkId)
      {
        return player;
      }
    }

    return null;
  }

  private Rope? FindRope(int networkId)
  {
    foreach (Rope rope in Ropes)
    {
      if (rope.NetworkId == networkId)
      {
        return rope;
      }
    }

    return null;
  }

  private CheckpointFlag? FindCheckpoint(int id)
  {
    foreach (CheckpointFlag checkpoint in Level.CheckpointFlags)
    {
      if (checkpoint.Id == id)
      {
        return checkpoint;
      }
    }

    return null;
  }
}

internal static class ReplayWorldFactory
{
  public static void SpawnPlayersFromFirstFrame(
    PlayerManager playerManager,
    GameSession session,
    ReplayFrameSnapshot firstFrame)
  {
    playerManager.Players.Clear();
    session.ClearPlayers();

    for (int i = 0; i < firstFrame.Players.Length; i++)
    {
      PlayerSnapshot snapshot = firstFrame.Players[i];
      session.ReserveNetworkId(snapshot.NetworkId);

      NetworkEntityOwnership ownership = new(
        snapshot.NetworkId,
        snapshot.OwnerId,
        IsLocal: true,
        IsHostControlled: false);

      Player player = new(
        snapshot.PlayerId,
        snapshot.PlayerIndex,
        new PartyMemberId(i + 1),
        snapshot.Position.ToVector2(),
        InputDevice.None,
        ownership,
        $"P{snapshot.PlayerIndex + 1}");

      player.ApplySnapshot(snapshot);
      playerManager.Players.Add(player);

      session.RegisterPlayer(new PlayerSessionInfo(
        snapshot.NetworkId,
        snapshot.PlayerId,
        snapshot.PlayerIndex,
        snapshot.OwnerId,
        isLocal: true,
        isHostControlled: false,
        InputDevice.None,
        $"P{snapshot.PlayerIndex + 1}",
        new PartyMemberId(i + 1)));
    }
  }

  public static void ConfigureRopes(PhysicsWorld physicsWorld, ReplayFrameSnapshot firstFrame)
  {
    for (int i = 0; i < physicsWorld.Ropes.Count && i < firstFrame.Ropes.Length; i++)
    {
      RopeSnapshot ropeSnapshot = firstFrame.Ropes[i];
      Rope rope = physicsWorld.Ropes[i];
      rope.ConfigureNetworkOwnership(new NetworkEntityOwnership(
        ropeSnapshot.NetworkId,
        ropeSnapshot.OwnerId,
        IsLocal: true,
        IsHostControlled: false));
      rope.ApplySnapshot(ropeSnapshot);
      rope.RefreshVisualState();
    }
  }
}
