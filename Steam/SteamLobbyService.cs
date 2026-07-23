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
    private readonly HashSet<ulong> _buildMismatchNotified = new();
    private CSteamID _currentLobby;
    private bool _isCreatingLobby;
    private ulong _pendingJoinLobbyId;
    private bool _openInviteWhenReady;
    private PartyStartMessage? _pendingLevelStart;

    public SteamLobbyService(SteamManager steam, SteamCallbackManager callbacks)
    {
        _steam = steam;
        _callbacks = callbacks;
        _callbacks.LobbyCreated += OnLobbyCreated;
        _callbacks.LobbyEnter += OnLobbyEnter;
        _callbacks.LobbyChatUpdate += OnLobbyChatUpdate;
        _callbacks.LobbyDataUpdate += OnLobbyDataUpdate;
        _callbacks.LobbyInvite += OnLobbyInvite;
        _callbacks.LobbyMatchList += OnLobbyMatchList;
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
    /// <summary>Fired when any peer broadcasts LEAVE_LEVEL:&lt;steamId&gt; (guest quit mid-run).</summary>
    public event Action<ulong>? LevelLeaveReceived;
    public event Action<SteamPartyError, string>? ErrorOccurred;
    public event Action<ulong>? MemberLeft;

    /// <summary>
    /// Consumes a START that arrived while no scene was listening (ephemeral lobby chat).
    /// </summary>
    public bool TryConsumePendingLevelStart(out PartyStartMessage message)
    {
        if (_pendingLevelStart is null)
        {
            message = default;
            return false;
        }

        message = _pendingLevelStart.Value;
        _pendingLevelStart = null;
        return true;
    }

    public void ClearPendingLevelStart()
    {
        _pendingLevelStart = null;
    }

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

        if (_pendingJoinLobbyId != 0)
        {
            MultiplayerDebug.LogLobby($"EnsurePartyLobby skip — pendingJoin={_pendingJoinLobbyId}");
            return;
        }

        if (IsInLobby || _isCreatingLobby)
        {
            MultiplayerDebug.LogLobby($"EnsurePartyLobby skip (inLobby={IsInLobby} creating={_isCreatingLobby})");
            return;
        }

        _isCreatingLobby = true;
        MultiplayerDebug.LogLobby($"CreateLobby friends-only max={SteamConstants.MaxLobbyPlayers}");
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

        if (IsInLobby && CurrentLobbyId == lobbyId)
        {
            MultiplayerDebug.LogLobby($"JoinLobby ignored — already in lobby {lobbyId}");
            return;
        }

        if (IsInLobby)
        {
            MultiplayerDebug.LogLobby(
                $"JoinLobby leaving current lobby {CurrentLobbyId} before joining {lobbyId}");
            LeaveLobby();
        }

        if (_isCreatingLobby)
        {
            _pendingJoinLobbyId = lobbyId;
            MultiplayerDebug.LogLobby(
                $"JoinLobby deferred — create in flight, pendingJoin={lobbyId}");
            return;
        }

        _pendingJoinLobbyId = 0;
        MultiplayerDebug.LogLobby($"JoinLobby request id={lobbyId}");
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
            _openInviteWhenReady = true;
            MultiplayerDebug.LogLobby("InviteFriends deferred — waiting for lobby ready");
            EnsurePartyLobby();
            return;
        }

        OpenInviteOverlayNow();
    }

    private void OpenInviteOverlayNow()
    {
        _openInviteWhenReady = false;
        MultiplayerDebug.LogLobby($"ActivateGameOverlayInviteDialog lobby={_currentLobby.m_SteamID}");
        SteamFriends.ActivateGameOverlayInviteDialog(_currentLobby);
    }

    private void TryOpenDeferredInviteOverlay()
    {
        if (!_openInviteWhenReady || !IsInLobby)
        {
            return;
        }

        OpenInviteOverlayNow();
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
            MultiplayerDebug.LogLobby($"SetLocalMemberData '{key}' DROPPED — not in lobby");
            return;
        }

        string existing = SteamMatchmaking.GetLobbyMemberData(_currentLobby, SteamUser.GetSteamID(), key);
        if (string.Equals(existing, value, StringComparison.Ordinal))
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
        SetLobbyData(SteamConstants.LobbyDataBuildGuid, BuildInfo.Current.BuildGuid);
        SetLobbyData(SteamConstants.LobbyDataGitCommit, BuildInfo.Current.GitCommit);
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
        if (!IsInLobby || !IsLobbyOwner())
        {
            MultiplayerDebug.LogWarn(
                $"PublishPartyRoster DROPPED — inLobby={IsInLobby} owner={IsLobbyOwner()}");
        }

        SetLobbyData(SteamConstants.LobbyDataPartyRoster, rosterData);
    }

    /// <summary>
    /// Host-only: mark whether a GameScene session is active. Clients watch this to leave
    /// the level when the host backs to menu without leaving the Steam lobby.
    /// </summary>
    public void SetGameplayActive(bool active)
    {
        if (!IsInLobby || !IsLobbyOwner())
        {
            return;
        }

        string value = active ? "1" : "0";
        SetLobbyData(SteamConstants.LobbyDataGameplay, value);
        MultiplayerDebug.LogLobby($"SetGameplayActive={value}");
    }

    public bool IsGameplayActive =>
        IsInLobby && GetLobbyData(SteamConstants.LobbyDataGameplay) == "1";

    /// <summary>
    /// Any lobby member can signal they left the level. Host removes them from sim (run continues unofficial).
    /// Does not leave the Steam lobby.
    /// </summary>
    public void BroadcastLeaveLevel()
    {
        if (!IsInLobby)
        {
            return;
        }

        string message = $"{SteamConstants.ChatPrefixLeaveLevel}{LocalSteamId}";
        byte[] bytes = Encoding.UTF8.GetBytes(message);
        SteamMatchmaking.SendLobbyChatMsg(_currentLobby, bytes, bytes.Length);
        MultiplayerDebug.LogLobby($"BroadcastLeaveLevel from local={LocalSteamId}");
    }


    public bool ValidateLobbyVersion()
    {
        if (TryGetHostBuildMismatch(out string mismatchMessage))
        {
            ErrorOccurred?.Invoke(SteamPartyError.VersionMismatch, mismatchMessage);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Build handshake (client side): compares the host's GameVersion / Build GUID / Git Commit
    /// (published as lobby data) with this build. Any difference is a mismatch.
    /// </summary>
    public bool TryGetHostBuildMismatch(out string mismatchMessage)
    {
        mismatchMessage = string.Empty;
        string? hostVersion = GetLobbyData(SteamConstants.LobbyDataGameVersion);
        string? hostGuid = GetLobbyData(SteamConstants.LobbyDataBuildGuid);
        string? hostCommit = GetLobbyData(SteamConstants.LobbyDataGitCommit);
        BuildInfo local = BuildInfo.Current;

        bool anyPublished = !string.IsNullOrEmpty(hostVersion)
            || !string.IsNullOrEmpty(hostGuid)
            || !string.IsNullOrEmpty(hostCommit);
        if (!anyPublished)
        {
            return false;
        }

        bool mismatch =
            (!string.IsNullOrEmpty(hostVersion) && !string.Equals(hostVersion, local.GameVersion, StringComparison.Ordinal))
            || (!string.IsNullOrEmpty(hostGuid) && !string.Equals(hostGuid, local.BuildGuid, StringComparison.Ordinal))
            || (!string.IsNullOrEmpty(hostCommit) && !string.Equals(hostCommit, local.GitCommit, StringComparison.Ordinal));

        string hostLabel = FormatBuildLabel(hostVersion, hostGuid);
        if (!IsLobbyOwner())
        {
            SessionDiagnostics.RecordBuildHandshake(hostLabel, local.Label, !mismatch);
        }

        MultiplayerDebug.LogLobby(
            $"BuildHandshake host={hostVersion}/{hostGuid}/{hostCommit} " +
            $"local={local.GameVersion}/{local.BuildGuid}/{local.GitCommit} match={!mismatch}");

        if (!mismatch)
        {
            return false;
        }

        mismatchMessage = $"Version mismatch detected. Host: {hostLabel} Client: {local.Label}";
        MultiplayerDebug.LogError(
            "BuildHandshake",
            $"Version mismatch detected. Host: {hostVersion} ({hostGuid}) commit={hostCommit} " +
            $"Client: {local.GameVersion} ({local.BuildGuid}) commit={local.GitCommit}");
        return true;
    }

    /// <summary>
    /// Build handshake (host side): every member publishes its build via member data.
    /// Returns true when a member's build differs from the host build.
    /// </summary>
    public bool ValidateMemberBuilds()
    {
        if (!IsInLobby || !IsLobbyOwner())
        {
            return false;
        }

        BuildInfo local = BuildInfo.Current;
        bool anyMismatch = false;
        foreach (LobbyMemberInfo member in GetLobbyMembers())
        {
            if (member.SteamId == LocalSteamId)
            {
                continue;
            }

            string? token = GetLobbyMemberData(member.SteamId, SteamConstants.LobbyMemberDataBuild);
            if (string.IsNullOrEmpty(token))
            {
                continue;
            }

            string[] parts = token.Split('|');
            string clientVersion = parts.Length > 0 ? parts[0] : "?";
            string clientGuid = parts.Length > 1 ? parts[1] : "?";
            string clientCommit = parts.Length > 2 ? parts[2] : "?";
            bool match = string.Equals(token, local.HandshakeToken, StringComparison.Ordinal);
            string clientLabel = FormatBuildLabel(clientVersion, clientGuid);
            SessionDiagnostics.RecordBuildHandshake(local.Label, clientLabel, match);

            if (match)
            {
                _buildMismatchNotified.Remove(member.SteamId);
                continue;
            }

            anyMismatch = true;
            if (_buildMismatchNotified.Add(member.SteamId))
            {
                MultiplayerDebug.LogError(
                    "BuildHandshake",
                    $"Version mismatch detected. Host: {local.GameVersion} ({local.BuildGuid}) commit={local.GitCommit} " +
                    $"Client '{member.DisplayName}': {clientVersion} ({clientGuid}) commit={clientCommit}");
                ErrorOccurred?.Invoke(
                    SteamPartyError.VersionMismatch,
                    $"Version mismatch detected. Host: {local.Label} Client: {clientLabel}");
            }
        }

        return anyMismatch;
    }

    /// <summary>Returns false when the start was cancelled (e.g. build mismatch across peers).</summary>
    public bool BroadcastLevelStart(string levelId, RopeGameplayMode ropeMode, bool lavaRiseEnabled)
    {
        if (!IsInLobby || !IsLobbyOwner())
        {
            return true;
        }

        MultiplayerDebug.LogLobby($"StartLevelRequested level={levelId} members={GetLobbyMemberCount()}");
        if (ValidateMemberBuilds())
        {
            MultiplayerDebug.LogError("BuildHandshake", "StartLevelRequested CANCELLED — build mismatch in lobby");
            return false;
        }

        PublishLobbySettings(levelId, ropeMode, lavaRiseEnabled);
        string levelHash = SessionDiagnostics.ComputeLevelHash(levelId);
        SetLobbyData(SteamConstants.LobbyDataLevelHash, levelHash);
        SessionDiagnostics.RecordLevelHashes(levelHash, levelHash);
        string message =
            $"{SteamConstants.ChatPrefixStart}{levelId}|{(int)ropeMode}|{(lavaRiseEnabled ? 1 : 0)}|{levelHash}";
        byte[] bytes = Encoding.UTF8.GetBytes(message);
        MultiplayerDebug.LogLobby(
            $"BroadcastLevelStart level={levelId} rope={ropeMode} lava={lavaRiseEnabled} " +
            $"hash={SessionDiagnostics.ShortHash(levelHash)} members={GetLobbyMemberCount()}");
        SteamMatchmaking.SendLobbyChatMsg(_currentLobby, bytes, bytes.Length);
        return true;
    }

    private static string FormatBuildLabel(string? version, string? buildGuid)
    {
        string shortId = string.IsNullOrEmpty(buildGuid)
            ? "??????"
            : (buildGuid.Length >= 6 ? buildGuid[..6] : buildGuid);
        return $"{(string.IsNullOrEmpty(version) ? "?" : version)} ({shortId})";
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
            if (_pendingJoinLobbyId != 0)
            {
                ulong pending = _pendingJoinLobbyId;
                _pendingJoinLobbyId = 0;
                MultiplayerDebug.LogLobby($"OnLobbyCreated failed → JoinLobby pending={pending}");
                JoinLobby(pending);
            }

            return;
        }

        ulong createdId = callback.m_ulSteamIDLobby;
        if (_pendingJoinLobbyId != 0)
        {
            ulong pending = _pendingJoinLobbyId;
            _pendingJoinLobbyId = 0;
            MultiplayerDebug.LogLobby(
                $"OnLobbyCreated abandon own lobby={createdId} → JoinLobby pending={pending}");
            SteamMatchmaking.LeaveLobby(new CSteamID(createdId));
            ClearLobbyState();
            LobbyStateChanged?.Invoke();
            JoinLobby(pending);
            return;
        }

        _currentLobby = new CSteamID(createdId);
        MultiplayerDebug.LogLobby($"OnLobbyCreated ok id={_currentLobby.m_SteamID}");

        string sessionId = DiagnosticsLog.CreateSessionId(_currentLobby.m_SteamID);
        SetLobbyData(SteamConstants.LobbyDataSessionId, sessionId);
        DiagnosticsLog.SetSessionId(sessionId);

        InitializeLobbyDefaults();
        PublishLocalBuildInfo();
        LobbyStateChanged?.Invoke();
        LobbyReady?.Invoke();
        TryOpenDeferredInviteOverlay();
    }

    private void OnLobbyEnter(LobbyEnter_t callback)
    {
        _isCreatingLobby = false;
        if ((EChatRoomEnterResponse)callback.m_EChatRoomEnterResponse != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            MultiplayerDebug.LogLobby($"OnLobbyEnter FAIL response={callback.m_EChatRoomEnterResponse}");
            ErrorOccurred?.Invoke(SteamPartyError.JoinFailed, $"Join failed: {callback.m_EChatRoomEnterResponse}");
            return;
        }

        _currentLobby = new CSteamID(callback.m_ulSteamIDLobby);
        if (_pendingJoinLobbyId != 0 && _pendingJoinLobbyId != _currentLobby.m_SteamID)
        {
            // Entered an unexpected lobby while a join was pending — abandon and retry pending.
            ulong pending = _pendingJoinLobbyId;
            _pendingJoinLobbyId = 0;
            MultiplayerDebug.LogLobby(
                $"OnLobbyEnter unexpected id={_currentLobby.m_SteamID} → leave + JoinLobby pending={pending}");
            LeaveLobby();
            JoinLobby(pending);
            return;
        }

        _pendingJoinLobbyId = 0;
        if (!ValidateLobbyVersion())
        {
            MultiplayerDebug.LogLobby($"OnLobbyEnter version mismatch → leave id={_currentLobby.m_SteamID}");
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

        MultiplayerDebug.LogLobby(
            $"OnLobbyEnter ok id={_currentLobby.m_SteamID} members={memberCount}/{memberLimit} local={LocalSteamId}");
        AdoptLobbySessionId();
        PublishLocalBuildInfo();
        LogLobbyMemberList("enter");
        RefreshMetadataFromLobby();
        LobbyStateChanged?.Invoke();
        LobbyReady?.Invoke();
        TryOpenDeferredInviteOverlay();
    }

    /// <summary>Client reply half of the build handshake: publish this build as lobby member data.</summary>
    private void PublishLocalBuildInfo()
    {
        SetLocalMemberData(SteamConstants.LobbyMemberDataBuild, BuildInfo.Current.HandshakeToken);
    }

    private void AdoptLobbySessionId()
    {
        string? sessionId = GetLobbyData(SteamConstants.LobbyDataSessionId);
        if (!string.IsNullOrEmpty(sessionId))
        {
            DiagnosticsLog.SetSessionId(sessionId);
        }
    }

    private void OnLobbyChatUpdate(LobbyChatUpdate_t callback)
    {
        if (!IsInLobby || callback.m_ulSteamIDLobby != _currentLobby.m_SteamID)
        {
            return;
        }

        EChatMemberStateChange state = (EChatMemberStateChange)callback.m_rgfChatMemberStateChange;
        MultiplayerDebug.LogLobby(
            $"OnLobbyChatUpdate user={callback.m_ulSteamIDUserChanged} state={state} members={GetLobbyMemberCount()}");
        if (state.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeLeft)
            || state.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeDisconnected)
            || state.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeKicked)
            || state.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeBanned))
        {
            MemberLeft?.Invoke(callback.m_ulSteamIDUserChanged);
        }

        LogLobbyMemberList("chat-update");
        LobbyStateChanged?.Invoke();
    }

    private void OnLobbyDataUpdate(LobbyDataUpdate_t callback)
    {
        if (!IsInLobby || callback.m_ulSteamIDLobby != _currentLobby.m_SteamID)
        {
            return;
        }

        RefreshMetadataFromLobby();
        AdoptLobbySessionId();
        ValidateMemberBuilds();

        bool isLobbyMetadata = callback.m_ulSteamIDMember == callback.m_ulSteamIDLobby;
        // Own member-data writes must not re-enter LobbyStateChanged → ForceSync → PublishLocalMemberData
        // (that loop flooded logs and froze/crashed Party open).
        bool isOwnMemberData = !isLobbyMetadata && callback.m_ulSteamIDMember == LocalSteamId;
        bool willFire = isLobbyMetadata || (IsLobbyOwner() && !isOwnMemberData);
        MultiplayerDebug.LogLobby(
            $"OnLobbyDataUpdate source={(isLobbyMetadata ? "LOBBY-METADATA" : $"MEMBER {callback.m_ulSteamIDMember}")} " +
            $"→ LobbyStateChanged={(willFire ? "FIRE" : (isOwnMemberData ? "SUPPRESSED (own member data)" : "SUPPRESSED (non-owner ignores member-data updates)"))}");
        if (willFire)
        {
            LobbyStateChanged?.Invoke();
        }
    }

    private void OnLobbyInvite(LobbyInvite_t callback)
    {
        // Notification only — join happens on GameLobbyJoinRequested (user Accept) via SteamInviteManager.
        MultiplayerDebug.LogLobby(
            $"InviteReceived lobby={callback.m_ulSteamIDLobby} from={callback.m_ulSteamIDUser} (await Accept)");
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

        if (message.StartsWith(SteamConstants.ChatPrefixLeaveLevel, StringComparison.Ordinal))
        {
            string payload = message[SteamConstants.ChatPrefixLeaveLevel.Length..];
            ulong leaveSteamId = LocalSteamId;
            if (!ulong.TryParse(payload, out leaveSteamId) || leaveSteamId == 0)
            {
                // Legacy bare LEAVE_LEVEL: cannot map peer — ignore for despawn.
                MultiplayerDebug.LogWarn($"LevelLeaveReceived missing steam id payload='{payload}'");
                return;
            }

            MultiplayerDebug.LogLobby($"LevelLeaveReceived steam={leaveSteamId}");
            LevelLeaveReceived?.Invoke(leaveSteamId);
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

        string? levelHash = parts.Length >= 4 && !string.IsNullOrWhiteSpace(parts[3]) ? parts[3] : null;
        PartyStartMessage startMessage = new(parts[0], (RopeGameplayMode)ropeMode, lavaRise == 1, levelHash);
        _pendingLevelStart = startMessage;
        LevelStartReceived?.Invoke(startMessage);
        MultiplayerDebug.LogLobby(
            $"StartLevelReceived level={parts[0]} rope={(RopeGameplayMode)ropeMode} lava={lavaRise == 1} " +
            $"hash={SessionDiagnostics.ShortHash(levelHash ?? string.Empty)}");
    }

    private void LogLobbyMemberList(string reason)
    {
        IReadOnlyList<LobbyMemberInfo> members = GetLobbyMembers();
        MultiplayerDebug.LogLobby($"MemberList ({reason}) count={members.Count}");
        foreach (LobbyMemberInfo member in members)
        {
            string owner = member.IsOwner ? " OWNER" : string.Empty;
            string self = member.SteamId == LocalSteamId ? " YOU" : string.Empty;
            MultiplayerDebug.LogLobby($"  member '{member.DisplayName}' sid={member.SteamId}{owner}{self}");
        }
    }

    private void InitializeLobbyDefaults()
    {
        PublishLobbySettings(null, RopeGameplayMode.ColoredPhysics, false);
        SetGameplayActive(false);
    }

    // Rich Presence set/clear lives in SteamInviteManager, driven by LobbyStateChanged.
    private void ClearLobbyState()
    {
        _currentLobby = CSteamID.Nil;
        CurrentLevelId = null;
        CurrentLavaRiseEnabled = false;
        CurrentRopeMode = RopeGameplayMode.ColoredPhysics;
        _pendingLevelStart = null;
        _buildMismatchNotified.Clear();
        DiagnosticsLog.ResetSessionId();
        SessionDiagnostics.ResetSessionState();
    }
}
