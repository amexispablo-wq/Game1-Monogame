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
        InputManager input,
        SteamLobbyService? lobby = null)
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
            bool isHostControlled = _session.IsHost;
            string indicatorLabel = member.DisplayName;

            int networkId = member.NetworkPlayerId > 0
                ? member.NetworkPlayerId
                : _session.AllocateNetworkId();
            if (member.NetworkPlayerId > 0)
            {
                _session.ReserveNetworkId(networkId);
            }
            NetworkEntityOwnership ownership = new(networkId, ownerId, isLocal, isHostControlled);
            string role = isLocal ? "LocalPlayer" : "RemotePlayer";
            MultiplayerDebug.LogSpawn(
                $"Create {role} '{indicatorLabel}' idx={i} NetworkId={networkId} OwnerId={ownerId} " +
                $"hostCtrl={isHostControlled} input={assignedInput.DeviceType} " +
                $"(from party NetworkPlayerId={member.NetworkPlayerId} — no spawn packet)");
            Player player = new(
                GetPlayerId(i),
                i,
                member.Id,
                spawnPosition,
                assignedInput,
                ownership,
                indicatorLabel);
            Players.Add(player);
            ApplySpawnColor(player);

            (PlayerSkinData? skin, string? skinId) = PlayerSkinCodec.ResolveForMember(lobby, member, members);
            if (skin is not null)
            {
                player.SetCosmeticSkin(skin, skinId);
                MultiplayerDebug.LogSpawn(
                    $"Skin applied '{indicatorLabel}' local={isLocal} skinId='{skinId ?? ""}'");
            }
            else
            {
                MultiplayerDebug.LogSpawn($"Skin missing '{indicatorLabel}' local={isLocal}");
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
            MultiplayerDebug.LogSpawn(
                $"Register NetworkId={networkId} OwnerId={ownerId} sessionPlayers={_session.Players.Count}");
        }

        input.SetGameplayBindings(bindings);
        MultiplayerDebug.LogSpawn($"SpawnFromParty done players={Players.Count} role={_session.Role}");
        return Players;
    }

    public IReadOnlyList<Player> SpawnSoloTest(
        IReadOnlyList<PartyMember> partyMembers,
        InputManager input)
    {
        List<PartyMember> solo = new(1);
        for (int i = 0; i < partyMembers.Count; i++)
        {
            if (!partyMembers[i].IsLocallyOwned)
            {
                continue;
            }

            solo.Add(partyMembers[i]);
            break;
        }

        return SpawnFromParty(solo, input);
    }

    /// <summary>
    /// Remove all players owned by <paramref name="ownerId"/> (one Steam peer, possibly splitscreen).
    /// Returns how many players removed.
    /// </summary>
    public int RemovePlayersByOwnerId(int ownerId)
    {
        int removed = 0;
        for (int i = Players.Count - 1; i >= 0; i--)
        {
            Player player = Players[i];
            if (player.OwnerId != ownerId)
            {
                continue;
            }

            _session.UnregisterPlayer(player.NetworkId);
            Players.RemoveAt(i);
            removed++;
        }

        if (removed > 0)
        {
            MultiplayerDebug.LogSpawn(
                $"RemovePlayersByOwnerId owner={ownerId} removed={removed} remaining={Players.Count}");
        }

        return removed;
    }

    public void SyncInputDevicesFromParty(IReadOnlyList<PartyMember> members, InputManager input)
    {
        Dictionary<int, PartyMember> bindings = new();
        for (int i = 0; i < Players.Count; i++)
        {
            Player player = Players[i];
            PartyMember? member = null;
            for (int m = 0; m < members.Count; m++)
            {
                if (members[m].Id == player.PartyMemberId)
                {
                    member = members[m];
                    break;
                }
            }

            if (member is null)
            {
                continue;
            }

            if (member.IsLocallyOwned)
            {
                player.AssignedInput = ToInputDevice(member);
            }

            bindings[player.NetworkId] = member;
        }

        input.SetGameplayBindings(bindings);
    }

    public void ActivateCheckpoint(CheckpointFlag checkpoint, bool playSfx = true)
    {
        // Same flag only arms once — revisiting must not re-capture / re-set.
        if (ReferenceEquals(CurrentCheckpoint, checkpoint))
        {
            return;
        }

        if (CurrentCheckpoint is not null)
        {
            CurrentCheckpoint.IsActive = false;
        }

        CurrentCheckpoint = checkpoint;
        CurrentCheckpoint.IsActive = true;
        CaptureCheckpointPlayerColors();
        if (playSfx)
        {
            GameAudio.Play(SfxManager.Checkpoint);
        }
    }

    public void RespawnPlayer(Player player)
    {
        player.RespawnAt(RespawnPosition);
        if (HasCheckpoint)
        {
            ApplyCheckpointColor(player);
        }
        else
        {
            ApplySpawnColor(player);
        }
    }

    public void ReviveAllAtStart()
    {
        for (int i = 0; i < Players.Count; i++)
        {
            Players[i].Revive(GetSpawnPosition(i));
            ApplySpawnColor(Players[i]);
        }
    }

    public void ReviveAllAtCheckpoint()
    {
        Vector2 basePosition = RespawnPosition;
        for (int i = 0; i < Players.Count; i++)
        {
            // Spread same-color stacks so player-collision eject does not loop vertically.
            Players[i].Revive(basePosition + new Vector2(PlayerSpawnSpacing * i, 0f));
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

    private void ApplySpawnColor(Player player)
    {
        player.RestoreColor(Level.NormalizePlayerStartColor(_level.PlayerStartColor));
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
        ApplySpawnColor(player);

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
