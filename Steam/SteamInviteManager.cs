#nullable enable
using System;
using Steamworks;

namespace ColorBlocks;

/// <summary>
/// Single owner of Steam invite + join flows:
/// - Invite overlay (existing in-game "Invite Friends" button).
/// - Rich Presence publication so friends (including those set to Invisible) get "Join Game".
/// - External join requests (friends panel Invite to Game / Join Game), both while
///   running (GameLobbyJoinRequested / GameRichPresenceJoinRequested) and via cold-start
///   launch arguments.
/// Gameplay never touches this; scenes call through ColorBlocksGame.
/// </summary>
public sealed class SteamInviteManager
{
    private readonly SteamManager _steam;
    private readonly SteamLobbyService _lobby;
    private bool _presenceSet;

    public SteamInviteManager(
        SteamManager steam,
        SteamCallbackManager callbacks,
        SteamLobbyService lobby)
    {
        _steam = steam;
        _lobby = lobby;
        callbacks.GameRichPresenceJoinRequested += OnGameRichPresenceJoinRequested;
        callbacks.GameLobbyJoinRequested += OnGameLobbyJoinRequested;
        _lobby.LobbyStateChanged += SyncPresenceToLobbyState;
        JoinRequestCallbackActive = true;
        MultiplayerDebug.JoinRequestCallbackActive = true;
    }

    public bool JoinRequestCallbackActive { get; }
    public string CurrentConnectString { get; private set; } = string.Empty;
    public bool IsPresencePublished => _presenceSet;

    /// <summary>Existing in-game "Invite Friends" button path.</summary>
    public void OpenInviteOverlay()
    {
        MultiplayerDebug.LogLobby("OpenInviteOverlay → Steam overlay invite dialog");
        _lobby.InviteFriends();
    }

    /// <summary>Publish connect info for the current lobby. Data only, no join logic.</summary>
    public void SetLobbyPresence()
    {
        if (!_steam.IsInitialized || !_lobby.IsInLobby)
        {
            return;
        }

        ulong lobbyId = _lobby.CurrentLobbyId;
        string connect = $"{SteamConstants.RichPresenceConnectPrefix}{lobbyId}";
        if (_presenceSet && string.Equals(CurrentConnectString, connect, StringComparison.Ordinal))
        {
            // Still refresh group size in case roster grew without connect change.
            SteamFriends.SetRichPresence(
                SteamConstants.RichPresencePlayerGroupSizeKey,
                Math.Max(1, _lobby.GetLobbyMemberCount()).ToString());
            return;
        }

        SteamFriends.SetRichPresence(SteamConstants.RichPresenceConnectKey, connect);
        SteamFriends.SetRichPresence(
            SteamConstants.RichPresenceDisplayKey,
            SteamConstants.RichPresenceInPartyToken);
        // Party group keys let Steam UI cluster friends who share this lobby.
        SteamFriends.SetRichPresence(SteamConstants.RichPresencePlayerGroupKey, lobbyId.ToString());
        SteamFriends.SetRichPresence(
            SteamConstants.RichPresencePlayerGroupSizeKey,
            Math.Max(1, _lobby.GetLobbyMemberCount()).ToString());
        _presenceSet = true;
        CurrentConnectString = connect;
        MultiplayerDebug.RichPresenceConnect = connect;
        MultiplayerDebug.LogLobby(
            $"RichPresence published connect='{connect}' display='{SteamConstants.RichPresenceInPartyToken}' " +
            $"group={lobbyId} size={_lobby.GetLobbyMemberCount()}");
    }

    public void ClearPresence()
    {
        if (!_steam.IsInitialized || !_presenceSet)
        {
            return;
        }

        SteamFriends.ClearRichPresence();
        _presenceSet = false;
        CurrentConnectString = string.Empty;
        MultiplayerDebug.RichPresenceConnect = string.Empty;
        MultiplayerDebug.LogLobby("RichPresence cleared");
    }

    /// <summary>
    /// Friends-panel "Invite to Game" accepted → GameLobbyJoinRequested.
    /// Same leave / cancel-create path as Rich Presence Join Game.
    /// </summary>
    public void AcceptLobbyInvite(ulong lobbyId)
    {
        MultiplayerDebug.LogLobby($"AcceptLobbyInvite lobby={lobbyId}");
        if (lobbyId == 0)
        {
            MultiplayerDebug.LogWarn("AcceptLobbyInvite ignored — lobby id 0");
            return;
        }

        if (_lobby.IsInLobby && _lobby.CurrentLobbyId == lobbyId)
        {
            MultiplayerDebug.LogLobby($"AcceptLobbyInvite ignored — already in lobby {lobbyId}");
            return;
        }

        SteamMatchmaking.RequestLobbyData(new CSteamID(lobbyId));
        _lobby.JoinLobby(lobbyId);
    }

    /// <summary>Validate + route an external join request to the existing lobby system.</summary>
    public void HandleJoinRequest(string connect)
    {
        MultiplayerDebug.LogLobby($"JoinRequest received connect='{connect}'");
        if (!TryParseLobbyId(connect, out ulong lobbyId))
        {
            MultiplayerDebug.LogWarn($"JoinRequest invalid connect string '{connect}' — ignored");
            return;
        }

        AcceptLobbyInvite(lobbyId);
    }

    /// <summary>
    /// Cold start: friend clicked "Join Game" while our game was closed. Steam launches the
    /// game with the connect string (or +connect_lobby &lt;id&gt;) on the command line.
    /// Call once after Steam init.
    /// </summary>
    public void TryConsumeLaunchJoin(string[] commandLineArgs)
    {
        if (!_steam.IsInitialized || commandLineArgs is null || commandLineArgs.Length == 0)
        {
            return;
        }

        for (int i = 0; i < commandLineArgs.Length; i++)
        {
            string arg = commandLineArgs[i];
            if (string.IsNullOrWhiteSpace(arg))
            {
                continue;
            }

            // Single-token forms: "lobby:123" or "+connect_lobby_123"
            if (arg.StartsWith(SteamConstants.RichPresenceConnectPrefix, StringComparison.OrdinalIgnoreCase)
                || arg.StartsWith(SteamConstants.RichPresenceLegacyConnectPrefix, StringComparison.OrdinalIgnoreCase))
            {
                MultiplayerDebug.LogLobby($"Launch-arg join detected '{arg}'");
                HandleJoinRequest(arg);
                return;
            }

            // Steam classic two-token form: "+connect_lobby" "123456789"
            if (string.Equals(arg, SteamConstants.RichPresenceConnectLobbyFlag, StringComparison.OrdinalIgnoreCase)
                && i + 1 < commandLineArgs.Length
                && ulong.TryParse(commandLineArgs[i + 1], out ulong lobbyId)
                && lobbyId != 0)
            {
                string connect = $"{SteamConstants.RichPresenceConnectPrefix}{lobbyId}";
                MultiplayerDebug.LogLobby($"Launch-arg join detected '{arg} {lobbyId}'");
                HandleJoinRequest(connect);
                return;
            }
        }
    }

    private void SyncPresenceToLobbyState()
    {
        if (_lobby.IsInLobby)
        {
            SetLobbyPresence();
        }
        else
        {
            ClearPresence();
        }
    }

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        MultiplayerDebug.LogLobby(
            $"InviteAccepted lobby={callback.m_steamIDLobby.m_SteamID} friend={callback.m_steamIDFriend.m_SteamID}");
        AcceptLobbyInvite(callback.m_steamIDLobby.m_SteamID);
    }

    private void OnGameRichPresenceJoinRequested(GameRichPresenceJoinRequested_t callback)
    {
        MultiplayerDebug.LogLobby(
            $"GameRichPresenceJoinRequested friend={callback.m_steamIDFriend.m_SteamID} " +
            $"connect='{callback.m_rgchConnect}'");
        HandleJoinRequest(callback.m_rgchConnect);
    }

    private static bool TryParseLobbyId(string connect, out ulong lobbyId)
    {
        lobbyId = 0;
        if (string.IsNullOrWhiteSpace(connect))
        {
            return false;
        }

        ReadOnlySpan<char> span = connect.AsSpan().Trim();
        if (span.StartsWith(SteamConstants.RichPresenceConnectPrefix, StringComparison.OrdinalIgnoreCase))
        {
            span = span[SteamConstants.RichPresenceConnectPrefix.Length..];
        }
        else if (span.StartsWith(SteamConstants.RichPresenceLegacyConnectPrefix, StringComparison.OrdinalIgnoreCase))
        {
            span = span[SteamConstants.RichPresenceLegacyConnectPrefix.Length..];
        }
        else if (span.StartsWith(SteamConstants.RichPresenceConnectLobbyFlag, StringComparison.OrdinalIgnoreCase))
        {
            span = span[SteamConstants.RichPresenceConnectLobbyFlag.Length..];
            while (!span.IsEmpty && (span[0] == ' ' || span[0] == '=' || span[0] == ':'))
            {
                span = span[1..];
            }
        }

        return ulong.TryParse(span, out lobbyId) && lobbyId != 0;
    }
}
