using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace ColorBlocks;

public sealed class PlayerManager
{
    private const float PlayerSpawnSpacing = 50f;

    private readonly GameSession _session;
    private readonly Level _level;

    public PlayerManager(GameSession session, Level level)
    {
        _session = session;
        _level = level;
    }

    public List<Player> Players { get; } = new();
    public CheckpointFlag CurrentCheckpoint { get; private set; }
    public int? CurrentCheckpointId => CurrentCheckpoint?.Id;
    public Vector2 RespawnPosition => CurrentCheckpoint?.RespawnPosition ?? _level.PlayerStart;

    public IReadOnlyList<Player> SpawnLocalPlayers(IEnumerable<InputProfile> activeProfiles)
    {
        Players.Clear();
        _session.ClearPlayers();

        int spawnIndex = 0;
        foreach (InputProfile profile in activeProfiles)
        {
            Vector2 spawnPosition = GetSpawnPosition(spawnIndex);
            SpawnPlayer(
                profile.PlayerId,
                profile.PlayerIndex,
                spawnPosition,
                profile.AssignedInput,
                _session.LocalOwnerId,
                isLocal: true,
                isHostControlled: _session.IsHost,
                profile.DisplayName);
            spawnIndex++;
        }

        if (Players.Count == 0)
        {
            SpawnPlayer(
                PlayerId.Player1,
                0,
                _level.PlayerStart,
                InputDevice.Keyboard,
                _session.LocalOwnerId,
                isLocal: true,
                isHostControlled: _session.IsHost,
                "Player 1");
        }

        return Players;
    }

    public void ActivateCheckpoint(CheckpointFlag checkpoint)
    {
        if (ReferenceEquals(CurrentCheckpoint, checkpoint))
        {
            checkpoint.IsActive = true;
            return;
        }

        if (CurrentCheckpoint is not null)
        {
            CurrentCheckpoint.IsActive = false;
        }

        CurrentCheckpoint = checkpoint;
        CurrentCheckpoint.IsActive = true;
    }

    public void RespawnPlayer(Player player)
    {
        player.RespawnAt(RespawnPosition);
    }

    public Player SpawnRemotePlayer(
        PlayerId playerId,
        int playerIndex,
        int ownerId,
        string displayName)
    {
        Vector2 spawnPosition = GetSpawnPosition(Players.Count);
        return SpawnPlayer(
            playerId,
            playerIndex,
            spawnPosition,
            InputDevice.None,
            ownerId,
            isLocal: false,
            isHostControlled: _session.IsHost,
            displayName);
    }

    private Player SpawnPlayer(
        PlayerId playerId,
        int playerIndex,
        Vector2 spawnPosition,
        InputDevice assignedInput,
        int ownerId,
        bool isLocal,
        bool isHostControlled,
        string displayName)
    {
        int networkId = _session.AllocateNetworkId();
        NetworkEntityOwnership ownership = new(networkId, ownerId, isLocal, isHostControlled);
        Player player = new(playerId, playerIndex, spawnPosition, assignedInput, ownership);
        Players.Add(player);

        _session.RegisterPlayer(new PlayerSessionInfo(
            networkId,
            playerId,
            playerIndex,
            ownerId,
            isLocal,
            isHostControlled,
            assignedInput,
            displayName));

        return player;
    }

    private Vector2 GetSpawnPosition(int spawnIndex)
    {
        return _level.PlayerStart + new Vector2(PlayerSpawnSpacing * spawnIndex, 0f);
    }
}
