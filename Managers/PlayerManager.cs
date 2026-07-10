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
    private readonly Dictionary<int, GameColor> _checkpointPlayerColors = new();
    public CheckpointFlag CurrentCheckpoint { get; private set; }
    public int? CurrentCheckpointId => CurrentCheckpoint?.Id;
    public bool HasCheckpoint => CurrentCheckpoint is not null;
    public Vector2 RespawnPosition => CurrentCheckpoint?.RespawnPosition ?? _level.PlayerStart;

    public IReadOnlyList<Player> SpawnFromParty(
        IReadOnlyList<PartyMember> members,
        InputManager input)
    {
        Players.Clear();
        _session.ClearPlayers();

        Dictionary<int, PartyMember> bindings = new();
        int spawnIndex = 0;
        for (int i = 0; i < members.Count; i++)
        {
            PartyMember member = members[i];
            Vector2 spawnPosition = GetSpawnPosition(spawnIndex++);
            InputDevice assignedInput = ToInputDevice(member);
            bool isLocal = member.IsLocallyOwned;
            int ownerId = member.OwnerId != 0 ? member.OwnerId : _session.LocalOwnerId;
            bool isHostControlled = isLocal && _session.IsHost;
            string indicatorLabel = member.DisplayName;

            int networkId = member.NetworkPlayerId > 0
                ? member.NetworkPlayerId
                : _session.AllocateNetworkId();
            if (member.NetworkPlayerId > 0)
            {
                _session.ReserveNetworkId(networkId);
            }
            NetworkEntityOwnership ownership = new(networkId, ownerId, isLocal, isHostControlled);
            Player player = new(
                GetPlayerId(i),
                i,
                member.Id,
                spawnPosition,
                assignedInput,
                ownership,
                indicatorLabel);
            Players.Add(player);
            if (member.IsLocallyOwned)
            {
                string? skinId = SkinLibraryStorage.GetSelectedSkinId(member.Id);
                player.SetCosmeticSkin(SkinLibraryStorage.GetSkinForMember(member.Id), skinId);
            }

            bindings[networkId] = member;

            _session.RegisterPlayer(new PlayerSessionInfo(
                networkId,
                player.PlayerId,
                i,
                ownerId,
                isLocal,
                isHostControlled,
                assignedInput,
                member.DisplayName,
                member.Id));
        }

        input.SetGameplayBindings(bindings);
        return Players;
    }

    public void ActivateCheckpoint(CheckpointFlag checkpoint)
    {
        if (ReferenceEquals(CurrentCheckpoint, checkpoint))
        {
            checkpoint.IsActive = true;
            CaptureCheckpointPlayerColors();
            return;
        }

        if (CurrentCheckpoint is not null)
        {
            CurrentCheckpoint.IsActive = false;
        }

        CurrentCheckpoint = checkpoint;
        CurrentCheckpoint.IsActive = true;
        CaptureCheckpointPlayerColors();
    }

    public void RespawnPlayer(Player player)
    {
        player.RespawnAt(RespawnPosition);
        ApplyCheckpointColor(player);
    }

    public void ReviveAllAtStart()
    {
        for (int i = 0; i < Players.Count; i++)
        {
            Players[i].Revive(GetSpawnPosition(i));
        }
    }

    public void ReviveAllAtCheckpoint()
    {
        Vector2 position = RespawnPosition;
        foreach (Player player in Players)
        {
            player.Revive(position);
        }

        ApplyCheckpointPlayerColors();
    }

    public void ClearCheckpoint()
    {
        if (CurrentCheckpoint is not null)
        {
            CurrentCheckpoint.IsActive = false;
            CurrentCheckpoint = null;
        }

        _checkpointPlayerColors.Clear();
    }

    private void CaptureCheckpointPlayerColors()
    {
        _checkpointPlayerColors.Clear();
        foreach (Player player in Players)
        {
            _checkpointPlayerColors[player.NetworkId] = player.PlayerColor;
        }
    }

    private void ApplyCheckpointPlayerColors()
    {
        foreach (Player player in Players)
        {
            ApplyCheckpointColor(player);
        }
    }

    private void ApplyCheckpointColor(Player player)
    {
        if (_checkpointPlayerColors.TryGetValue(player.NetworkId, out GameColor color))
        {
            player.RestoreColor(color);
        }
    }

    public Player SpawnRemotePlayer(
        PlayerId playerId,
        int playerIndex,
        PartyMemberId partyMemberId,
        int ownerId,
        string displayName)
    {
        Vector2 spawnPosition = GetSpawnPosition(Players.Count);
        return SpawnPlayer(
            playerId,
            playerIndex,
            partyMemberId,
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
        PartyMemberId partyMemberId,
        Vector2 spawnPosition,
        InputDevice assignedInput,
        int ownerId,
        bool isLocal,
        bool isHostControlled,
        string displayName)
    {
        int networkId = _session.AllocateNetworkId();
        NetworkEntityOwnership ownership = new(networkId, ownerId, isLocal, isHostControlled);
        Player player = new(playerId, playerIndex, partyMemberId, spawnPosition, assignedInput, ownership, displayName);
        Players.Add(player);

        _session.RegisterPlayer(new PlayerSessionInfo(
            networkId,
            playerId,
            playerIndex,
            ownerId,
            isLocal,
            isHostControlled,
            assignedInput,
            displayName,
            partyMemberId));

        return player;
    }

    private static PlayerId GetPlayerId(int index)
    {
        return index switch
        {
            0 => PlayerId.Player1,
            1 => PlayerId.Player2,
            2 => PlayerId.Player3,
            3 => PlayerId.Player4,
            _ => PlayerId.Player1
        };
    }

    private static InputDevice ToInputDevice(PartyMember member)
    {
        return member.InputSource switch
        {
            PartyInputSource.Keyboard => InputDevice.Keyboard,
            PartyInputSource.Gamepad => InputDevice.Gamepad(member.ControllerId),
            _ => InputDevice.None
        };
    }

    private Vector2 GetSpawnPosition(int spawnIndex)
    {
        return _level.PlayerStart + new Vector2(PlayerSpawnSpacing * spawnIndex, 0f);
    }
}
