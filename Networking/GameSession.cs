using System.Collections.Generic;

namespace ColorBlocks;

public enum GameSessionRole
{
    LocalTest,
    Host,
    Client
}

public enum GameSessionState
{
    MainMenu,
    SteamLobby,
    Party,
    LevelSelect,
    Loading,
    Playing,
    Completed,
    Dead,
    Disconnected
}

public sealed class GameSession
{
    private readonly NetworkIdAllocator _networkIds = new();

    private GameSession(
        GameSessionRole role,
        int localOwnerId,
        SessionPeer host,
        string selectedLevelId,
        RopeGameplayMode ropeGameplayMode)
    {
        Role = role;
        LocalOwnerId = localOwnerId;
        Host = host;
        SelectedLevelId = selectedLevelId;
        RopeGameplayMode = ropeGameplayMode;
        Peers.Add(host);
    }

    public GameSessionRole Role { get; }
    public GameSessionState State { get; set; } = GameSessionState.Loading;
    public int LocalOwnerId { get; }
    public int HostOwnerId => Host.OwnerId;
    public bool IsHost => Role is GameSessionRole.LocalTest or GameSessionRole.Host;
    public bool IsLocalTest => Role == GameSessionRole.LocalTest;
    public SessionPeer Host { get; }
    public List<SessionPeer> Peers { get; } = new();
    public List<PlayerSessionInfo> Players { get; } = new();
    public string SelectedLevelId { get; set; }
    public RopeGameplayMode RopeGameplayMode { get; set; }
    public bool LavaRiseEnabled { get; set; }
    public GameSessionSettings Settings { get; set; } = GameSessionSettings.Default;

    public static GameSession CreateLocalTest(string selectedLevelId, RopeGameplayMode ropeGameplayMode)
    {
        return new GameSession(
            GameSessionRole.LocalTest,
            NetworkOwners.HostOwnerId,
            SessionPeer.LocalHost,
            selectedLevelId,
            ropeGameplayMode);
    }

    public static GameSession CreateOnline(
        GameSessionRole role,
        string selectedLevelId,
        RopeGameplayMode ropeGameplayMode,
        int localOwnerId,
        string localDisplayName)
    {
        SessionPeer localPeer = new(localOwnerId, localDisplayName, localOwnerId.ToString());
        GameSessionRole resolvedRole = role == GameSessionRole.Client ? GameSessionRole.Client : GameSessionRole.Host;
        return new GameSession(resolvedRole, localOwnerId, localPeer, selectedLevelId, ropeGameplayMode);
    }

    public int AllocateNetworkId()
    {
        return _networkIds.Allocate();
    }

    public void ReserveNetworkId(int networkId)
    {
        _networkIds.Reserve(networkId);
    }

    public void ClearPlayers()
    {
        Players.Clear();
    }

    public void RegisterPlayer(PlayerSessionInfo player)
    {
        _networkIds.Reserve(player.NetworkId);

        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].NetworkId == player.NetworkId)
            {
                Players[i] = player;
                return;
            }
        }

        Players.Add(player);
    }
}

public readonly record struct GameSessionSettings(
    int SimulationTicksPerSecond,
    int MaxPlayers,
    bool HostAuthoritativeRopes)
{
    public static GameSessionSettings Default { get; } = new(60, 4, true);
}

public readonly record struct SessionPeer(
    int OwnerId,
    string DisplayName,
    string ExternalUserId = "")
{
    public static SessionPeer LocalHost { get; } = new(NetworkOwners.HostOwnerId, "Local Host");
}

public sealed class PlayerSessionInfo
{
    public PlayerSessionInfo(
        int networkId,
        PlayerId playerId,
        int playerIndex,
        int ownerId,
        bool isLocal,
        bool isHostControlled,
        InputDevice assignedInput,
        string displayName,
        PartyMemberId partyMemberId)
    {
        NetworkId = networkId;
        PlayerId = playerId;
        PlayerIndex = playerIndex;
        OwnerId = ownerId;
        IsLocal = isLocal;
        IsHostControlled = isHostControlled;
        AssignedInput = assignedInput;
        DisplayName = displayName;
        PartyMemberId = partyMemberId;
    }

    public int NetworkId { get; }
    public PlayerId PlayerId { get; }
    public int PlayerIndex { get; }
    public int OwnerId { get; }
    public bool IsLocal { get; }
    public bool IsRemote => !IsLocal;
    public bool IsHostControlled { get; }
    public InputDevice AssignedInput { get; }
    public string DisplayName { get; }
    public PartyMemberId PartyMemberId { get; }
}
