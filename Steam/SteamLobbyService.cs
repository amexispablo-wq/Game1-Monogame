#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using Steamworks;

namespace ColorBlocks;

public sealed class SteamLobbyService
{
    private readonly SteamManager _steam;
    private readonly SteamCallbackManager _callbacks;
    private CSteamID _currentLobby;
    private bool _isCreatingLobby;

    public SteamLobbyService(SteamManager steam, SteamCallbackManager callbacks)
    {
        _steam = steam;
        _callbacks = callbacks;
        _callbacks.LobbyCreated += OnLobbyCreated;
        _callbacks.LobbyEnter += OnLobbyEnter;
        _callbacks.LobbyChatUpdate += OnLobbyChatUpdate;
        _callbacks.LobbyDataUpdate += OnLobbyDataUpdate;
        _callbacks.LobbyInvite += OnLobbyInvite;
        _callbacks.GameLobbyJoinRequested += OnGameLobbyJoinRequested;
        _callbacks.LobbyMatchList += OnLobbyMatchList;
        _callbacks.GameRichPresenceJoinRequested += OnGameRichPresenceJoinRequested;
        _callbacks.LobbyChatMsg += OnLobbyChatMsg;
    }

    public bool IsAvailable => _steam.IsInitialized;
    public bool IsInLobby => IsAvailable && _currentLobby.IsValid();
    public bool IsCreatingLobby => _isCreatingLobby;
    public ulong CurrentLobbyId => _currentLobby.m_SteamID;
    public ulong LocalSteamId => IsAvailable ? SteamUser.GetSteamID().m_SteamID : 0;
    public string? CurrentLevelId { get; private set; }
    public RopeGameplayMode CurrentRopeMode { get; private set; } = RopeGameplayMode.ColoredPhysics;
    public bool CurrentLavaRiseEnabled { get; private set; }

    public event Action? LobbyStateChanged;
    public event Action? LobbyReady;
    public event Action<PartyStartMessage>? LevelStartReceived;
    public event Action<SteamPartyError, string>? ErrorOccurred;
    public event Action<ulong>? MemberLeft;

    public bool IsLobbyOwner()
    {
        if (!IsInLobby)
        {
            return true;
        }

        return SteamMatchmaking.GetLobbyOwner(_currentLobby) == SteamUser.GetSteamID();
    }

    public ulong GetLobbyOwnerSteamId()
    {
        if (!IsInLobby)
        {
            return LocalSteamId;
        }

        return SteamMatchmaking.GetLobbyOwner(_currentLobby).m_SteamID;
    }

    public int GetLobbyMemberCount()
    {
        if (!IsInLobby)
        {
            return 1;
        }

        return SteamMatchmaking.GetNumLobbyMembers(_currentLobby);
    }

    public IReadOnlyList<LobbyMemberInfo> GetLobbyMembers()
    {
        List<LobbyMemberInfo> members = new();
        if (!IsInLobby)
        {
            if (IsAvailable)
            {
                members.Add(new LobbyMemberInfo(LocalSteamId, SteamFriends.GetPersonaName(), true));
            }

            return members;
        }

        CSteamID owner = SteamMatchmaking.GetLobbyOwner(_currentLobby);
        int count = SteamMatchmaking.GetNumLobbyMembers(_currentLobby);
        for (int i = 0; i < count; i++)
        {
            CSteamID memberId = SteamMatchmaking.GetLobbyMemberByIndex(_currentLobby, i);
            members.Add(new LobbyMemberInfo(
                memberId.m_SteamID,
                SteamFriends.GetFriendPersonaName(memberId),
                memberId == owner));
        }

        return members;
    }

    public string? GetLobbyMemberData(ulong steamId, string key)
    {
        if (!IsInLobby)
        {
            return null;
        }

        return SteamMatchmaking.GetLobbyMemberData(_currentLobby, new CSteamID(steamId), key);
    }

    public string? GetLobbyData(string key)
    {
        if (!IsInLobby)
        {
            return null;
        }

        return SteamMatchmaking.GetLobbyData(_currentLobby, key);
    }

    public void EnsurePartyLobby()
    {
        if (!IsAvailable)
        {
            ErrorOccurred?.Invoke(SteamPartyError.SteamOffline, "Steam is offline.");
            return;
        }

        if (IsInLobby || _isCreatingLobby)
        {
            return;
        }

        _isCreatingLobby = true;
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, SteamConstants.MaxLobbyPlayers);
    }

    public void JoinLobby(ulong lobbyId)
    {
        if (!IsAvailable)
        {
            ErrorOccurred?.Invoke(SteamPartyError.SteamOffline, "Steam is offline.");
            return;
        }

        if (lobbyId == 0)
        {
            ErrorOccurred?.Invoke(SteamPartyError.JoinFailed, "Invalid lobby.");
            return;
        }

        SteamMatchmaking.JoinLobby(new CSteamID(lobbyId));
    }

    public void LeaveLobby()
    {
        if (!IsInLobby)
        {
            return;
        }

        SteamMatchmaking.LeaveLobby(_currentLobby);
        ClearLobbyState();
        LobbyStateChanged?.Invoke();
    }

    public void InviteFriends()
    {
        if (!IsAvailable)
        {
            ErrorOccurred?.Invoke(SteamPartyError.SteamOffline, "Steam is offline.");
            return;
        }

        if (!IsInLobby)
        {
            EnsurePartyLobby();
            return;
        }

        SteamFriends.ActivateGameOverlayInviteDialog(_currentLobby);
    }

    public void SetLobbyData(string key, string value)
    {
        if (!IsInLobby || !IsLobbyOwner())
        {
            return;
        }

        SteamMatchmaking.SetLobbyData(_currentLobby, key, value);
    }

    public void SetLocalMemberData(string key, string value)
    {
        if (!IsInLobby)
        {
            return;
        }

        SteamMatchmaking.SetLobbyMemberData(_currentLobby, key, value);
    }

    public void PublishLobbySettings(string? levelId, RopeGameplayMode ropeMode, bool lavaRiseEnabled)
    {
        if (!IsInLobby || !IsLobbyOwner())
        {
            return;
        }

        CurrentLevelId = levelId;
        CurrentRopeMode = ropeMode;
        CurrentLavaRiseEnabled = lavaRiseEnabled;

        SetLobbyData(SteamConstants.LobbyDataPartyVersion, SteamConstants.PartyVersion.ToString());
        SetLobbyData(SteamConstants.LobbyDataGameVersion, SteamConstants.GameVersion);
        if (!string.IsNullOrEmpty(levelId))
        {
            SetLobbyData(SteamConstants.LobbyDataLevel, levelId);
        }

        SetLobbyData(SteamConstants.LobbyDataRopeMode, ((int)ropeMode).ToString());
        SetLobbyData(SteamConstants.LobbyDataLavaRise, lavaRiseEnabled ? "1" : "0");
        SetLobbyData(SteamConstants.LobbyDataLeaderSteam, GetLobbyOwnerSteamId().ToString());
    }

    public void PublishPartyRoster(string rosterData)
    {
        SetLobbyData(SteamConstants.LobbyDataPartyRoster, rosterData);
    }

    public bool ValidateLobbyVersion()
    {
        string? gameVersion = GetLobbyData(SteamConstants.LobbyDataGameVersion);
        if (!string.IsNullOrEmpty(gameVersion)
            && !string.Equals(gameVersion, SteamConstants.GameVersion, StringComparison.Ordinal))
        {
            ErrorOccurred?.Invoke(
                SteamPartyError.VersionMismatch,
                $"Version mismatch. Lobby: {gameVersion}, Local: {SteamConstants.GameVersion}");
            return false;
        }

        return true;
    }

    public void BroadcastLevelStart(string levelId, RopeGameplayMode ropeMode, bool lavaRiseEnabled)
    {
        if (!IsInLobby || !IsLobbyOwner())
        {
            return;
        }

        PublishLobbySettings(levelId, ropeMode, lavaRiseEnabled);
        string message = $"{SteamConstants.ChatPrefixStart}{levelId}|{(int)ropeMode}|{(lavaRiseEnabled ? 1 : 0)}";
        byte[] bytes = Encoding.UTF8.GetBytes(message);
        SteamMatchmaking.SendLobbyChatMsg(_currentLobby, bytes, bytes.Length);
    }

    public void RefreshMetadataFromLobby()
    {
        if (!IsInLobby)
        {
            return;
        }

        CurrentLevelId = GetLobbyData(SteamConstants.LobbyDataLevel);
        if (int.TryParse(GetLobbyData(SteamConstants.LobbyDataRopeMode), out int ropeMode))
        {
            CurrentRopeMode = (RopeGameplayMode)ropeMode;
        }

        CurrentLavaRiseEnabled = GetLobbyData(SteamConstants.LobbyDataLavaRise) == "1";
    }

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        _isCreatingLobby = false;
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            ErrorOccurred?.Invoke(SteamPartyError.CreateFailed, $"Lobby create failed: {callback.m_eResult}");
            return;
        }

        _currentLobby = new CSteamID(callback.m_ulSteamIDLobby);
        InitializeLobbyDefaults();
        LobbyStateChanged?.Invoke();
        LobbyReady?.Invoke();
    }

    private void OnLobbyEnter(LobbyEnter_t callback)
    {
        _isCreatingLobby = false;
        if ((EChatRoomEnterResponse)callback.m_EChatRoomEnterResponse != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            ErrorOccurred?.Invoke(SteamPartyError.JoinFailed, $"Join failed: {callback.m_EChatRoomEnterResponse}");
            return;
        }

        _currentLobby = new CSteamID(callback.m_ulSteamIDLobby);
        if (!ValidateLobbyVersion())
        {
            LeaveLobby();
            return;
        }

        int memberLimit = SteamMatchmaking.GetLobbyMemberLimit(_currentLobby);
        int memberCount = SteamMatchmaking.GetNumLobbyMembers(_currentLobby);
        if (memberCount > memberLimit)
        {
            ErrorOccurred?.Invoke(SteamPartyError.LobbyFull, "Lobby is full.");
            LeaveLobby();
            return;
        }

        UpdateRichPresence();
        RefreshMetadataFromLobby();
        LobbyStateChanged?.Invoke();
        LobbyReady?.Invoke();
    }

    private void OnLobbyChatUpdate(LobbyChatUpdate_t callback)
    {
        if (!IsInLobby || callback.m_ulSteamIDLobby != _currentLobby.m_SteamID)
        {
            return;
        }

        EChatMemberStateChange state = (EChatMemberStateChange)callback.m_rgfChatMemberStateChange;
        if (state.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeLeft)
            || state.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeDisconnected)
            || state.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeKicked)
            || state.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeBanned))
        {
            MemberLeft?.Invoke(callback.m_ulSteamIDUserChanged);
        }

        LobbyStateChanged?.Invoke();
    }

    private void OnLobbyDataUpdate(LobbyDataUpdate_t callback)
    {
        if (!IsInLobby || callback.m_ulSteamIDLobby != _currentLobby.m_SteamID)
        {
            return;
        }

        RefreshMetadataFromLobby();

        bool isLobbyMetadata = callback.m_ulSteamIDMember == callback.m_ulSteamIDLobby;
        if (isLobbyMetadata || IsLobbyOwner())
        {
            LobbyStateChanged?.Invoke();
        }
    }

    private void OnLobbyInvite(LobbyInvite_t callback)
    {
        JoinLobby(callback.m_ulSteamIDLobby);
    }

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        JoinLobby(callback.m_steamIDLobby.m_SteamID);
    }

    private void OnLobbyMatchList(LobbyMatchList_t callback)
    {
        if (callback.m_nLobbiesMatching == 0)
        {
            ErrorOccurred?.Invoke(SteamPartyError.JoinFailed, "No matching lobbies found.");
            return;
        }

        CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(0);
        JoinLobby(lobbyId.m_SteamID);
    }

    private void OnGameRichPresenceJoinRequested(GameRichPresenceJoinRequested_t callback)
    {
        string connect = callback.m_rgchConnect;
        if (connect.StartsWith("+connect_lobby_", StringComparison.OrdinalIgnoreCase)
            && ulong.TryParse(connect.AsSpan("+connect_lobby_".Length), out ulong lobbyId))
        {
            JoinLobby(lobbyId);
            return;
        }

        if (ulong.TryParse(connect, out ulong directLobbyId))
        {
            JoinLobby(directLobbyId);
        }
    }

    public void KickMember(ulong steamId)
    {
        if (!IsInLobby || !IsLobbyOwner() || steamId == 0 || steamId == LocalSteamId)
        {
            return;
        }

        string message = $"{SteamConstants.ChatPrefixKick}{steamId}";
        byte[] bytes = Encoding.UTF8.GetBytes(message);
        SteamMatchmaking.SendLobbyChatMsg(_currentLobby, bytes, bytes.Length);
    }

    private void OnLobbyChatMsg(LobbyChatMsg_t callback)
    {
        if (!IsInLobby || callback.m_ulSteamIDLobby != _currentLobby.m_SteamID)
        {
            return;
        }

        byte[] buffer = new byte[4096];
        int length = SteamMatchmaking.GetLobbyChatEntry(
            _currentLobby,
            (int)callback.m_iChatID,
            out CSteamID _,
            buffer,
            buffer.Length,
            out EChatEntryType _);

        if (length <= 0)
        {
            return;
        }

        string message = Encoding.UTF8.GetString(buffer, 0, length);
        if (message.StartsWith(SteamConstants.ChatPrefixKick, StringComparison.Ordinal))
        {
            string payload = message[SteamConstants.ChatPrefixKick.Length..];
            if (ulong.TryParse(payload, out ulong kickedSteamId) && kickedSteamId == LocalSteamId)
            {
                LeaveLobby();
                ErrorOccurred?.Invoke(SteamPartyError.LobbyClosed, "You were removed from the party.");
            }

            return;
        }

        if (!message.StartsWith(SteamConstants.ChatPrefixStart, StringComparison.Ordinal))
        {
            return;
        }

        string startPayload = message[SteamConstants.ChatPrefixStart.Length..];
        string[] parts = startPayload.Split('|');
        if (parts.Length < 3 || !int.TryParse(parts[1], out int ropeMode) || !int.TryParse(parts[2], out int lavaRise))
        {
            return;
        }

        LevelStartReceived?.Invoke(new PartyStartMessage(parts[0], (RopeGameplayMode)ropeMode, lavaRise == 1));
    }

    private void InitializeLobbyDefaults()
    {
        PublishLobbySettings(null, RopeGameplayMode.ColoredPhysics, false);
        UpdateRichPresence();
    }

    private void UpdateRichPresence()
    {
        if (!IsInLobby)
        {
            return;
        }

        SteamFriends.SetRichPresence(SteamConstants.RichPresenceConnectKey, $"+connect_lobby_{_currentLobby.m_SteamID}");
        SteamFriends.SetRichPresence("steam_display", "#StatusInParty");
    }

    private void ClearLobbyState()
    {
        _currentLobby = CSteamID.Nil;
        CurrentLevelId = null;
        CurrentLavaRiseEnabled = false;
        CurrentRopeMode = RopeGameplayMode.ColoredPhysics;
        SteamFriends.SetRichPresence(SteamConstants.RichPresenceConnectKey, string.Empty);
        SteamFriends.SetRichPresence("steam_display", string.Empty);
    }
}
